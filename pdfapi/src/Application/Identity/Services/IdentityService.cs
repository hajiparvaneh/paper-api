using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OtpNet;
using PaperAPI.Application.Email;
using PaperAPI.Application.Identity.Exceptions;
using PaperAPI.Application.Identity.Options;
using PaperAPI.Application.Identity.Repositories;
using PaperAPI.Application.Identity.Requests;
using PaperAPI.Application.Identity.Responses;
using PaperAPI.Application.Identity.Tokens;
using PaperAPI.Domain.Identity;

namespace PaperAPI.Application.Identity.Services;

public sealed class IdentityService : IIdentityService
{
    private readonly IUserRepository _userRepository;
    private readonly ITwoFactorChallengeRepository _twoFactorChallengeRepository;
    private readonly ITwoFactorRememberedDeviceRepository _twoFactorRememberedDeviceRepository;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IDataProtector _twoFactorProtector;
    private readonly TwoFactorOptions _twoFactorOptions;
    private readonly ILogger<IdentityService> _logger;
    private const int EmailVerificationTokenExpirationHours = 24;
    private const int EmailVerificationRequiredDays = 30;
    private static readonly TimeSpan VerificationEmailRateLimit = TimeSpan.FromMinutes(3);

    public IdentityService(
        IUserRepository userRepository,
        ITwoFactorChallengeRepository twoFactorChallengeRepository,
        ITwoFactorRememberedDeviceRepository twoFactorRememberedDeviceRepository,
        ITokenService tokenService,
        IEmailService emailService,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<TwoFactorOptions> twoFactorOptions,
        ILogger<IdentityService> logger)
    {
        _userRepository = userRepository;
        _twoFactorChallengeRepository = twoFactorChallengeRepository;
        _twoFactorRememberedDeviceRepository = twoFactorRememberedDeviceRepository;
        _tokenService = tokenService;
        _emailService = emailService;
        _twoFactorProtector = dataProtectionProvider.CreateProtector("PaperAPI.TwoFactorSecrets.v1");
        _twoFactorOptions = twoFactorOptions.Value;
        _logger = logger;
    }

    public async Task<AuthenticationResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        ValidatePassword(request.Password);

        var existing = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            throw new IdentityException("email_in_use", "An account with this email already exists.", HttpStatusCode.Conflict);
        }

        var now = DateTimeOffset.UtcNow;
        var verificationToken = GenerateVerificationToken();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = HashPassword(request.Password),
            IsEmailVerified = false,
            EmailVerificationToken = verificationToken,
            EmailVerificationTokenExpiresAt = now.AddHours(EmailVerificationTokenExpirationHours),
            LastVerificationEmailSentAt = now,
            TermsAcceptedAtUtc = now,
            PrivacyAcknowledgedAtUtc = now,
            DpaAcknowledgedAtUtc = now,
            AcceptedFromIp = request.AcceptanceIp,
            AcceptedUserAgent = request.UserAgent,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };

        await _userRepository.AddAsync(user, cancellationToken);

        // Send welcome and verification emails (fire and forget to not block registration)
        _ = Task.Run(async () =>
        {
            try
            {
                await SendWelcomeEmailAsync(email, CancellationToken.None);
                await SendVerificationEmailAsync(email, verificationToken, CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Log email sending errors but don't throw to prevent registration failure
                _logger.LogError(ex, "Failed to send registration emails for {Email}", email);
            }
        });

        var token = _tokenService.GenerateToken(user.Id, user.Email);
        return new AuthenticationResult
        {
            UserId = user.Id,
            Email = user.Email,
            Token = token
        };
    }

    public async Task<AuthenticationResult> LoginAsync(LoginRequest request, string? rememberDeviceToken, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash) || !VerifyPassword(user.PasswordHash, request.Password))
        {
            throw new IdentityException("invalid_credentials", "The provided credentials are invalid.", HttpStatusCode.Unauthorized);
        }

        if (user.IsDeleted)
        {
            throw new IdentityException("account_disabled", "This account is no longer active.", HttpStatusCode.Forbidden);
        }

        // Check if email verification is required (not verified and older than 30 days)
        if (!user.IsEmailVerified && IsEmailVerificationRequired(user))
        {
            throw new IdentityException("email_verification_required", "Please verify your email before logging in.", HttpStatusCode.Forbidden);
        }

        if (user.TwoFactorEnabled)
        {
            if (string.IsNullOrWhiteSpace(user.TwoFactorSecret))
            {
                throw new IdentityException(
                    "two_factor_unavailable",
                    "Two-factor authentication needs to be reset. Please contact support.",
                    HttpStatusCode.Conflict);
            }

            var now = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(rememberDeviceToken))
            {
                var tokenHash = HashToken(rememberDeviceToken);
                var device = await _twoFactorRememberedDeviceRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
                if (device is not null)
                {
                    if (device.ExpiresAt > now)
                    {
                        device.LastUsedAt = now;
                        await _twoFactorRememberedDeviceRepository.UpdateAsync(device, cancellationToken);

                        var rememberedToken = _tokenService.GenerateToken(user.Id, user.Email);
                        return new AuthenticationResult
                        {
                            UserId = user.Id,
                            Email = user.Email,
                            Token = rememberedToken
                        };
                    }

                    await _twoFactorRememberedDeviceRepository.DeleteAsync(device, cancellationToken);
                }
            }

            var challengeToken = GenerateSecureToken();
            var challenge = new TwoFactorChallenge
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = HashToken(challengeToken),
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(_twoFactorOptions.ChallengeMinutes)
            };

            await _twoFactorChallengeRepository.AddAsync(challenge, cancellationToken);

            return new AuthenticationResult
            {
                UserId = user.Id,
                Email = user.Email,
                TwoFactorRequired = true,
                TwoFactorToken = challengeToken
            };
        }

        var token = _tokenService.GenerateToken(user.Id, user.Email);
        return new AuthenticationResult
        {
            UserId = user.Id,
            Email = user.Email,
            Token = token
        };
    }

    public async Task<AuthenticationResult> VerifyTwoFactorLoginAsync(TwoFactorLoginRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new IdentityException("invalid_request", "Two-factor payload is required.", HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new IdentityException("invalid_request", "Two-factor token is required.", HttpStatusCode.BadRequest);
        }

        var code = RequireTwoFactorCode(request.Code, HttpStatusCode.Unauthorized);
        var tokenHash = HashToken(request.Token);
        var challenge = await _twoFactorChallengeRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (challenge is null)
        {
            throw new IdentityException("two_factor_expired", "Two-factor authentication expired. Please log in again.", HttpStatusCode.Unauthorized);
        }

        var now = DateTimeOffset.UtcNow;
        if (challenge.ExpiresAt <= now)
        {
            await _twoFactorChallengeRepository.DeleteAsync(challenge, cancellationToken);
            throw new IdentityException("two_factor_expired", "Two-factor authentication expired. Please log in again.", HttpStatusCode.Unauthorized);
        }

        var user = await _userRepository.GetByIdAsync(challenge.UserId, cancellationToken)
                   ?? throw new IdentityException("user_not_found", "User not found.", HttpStatusCode.NotFound);

        if (user.IsDeleted)
        {
            throw new IdentityException("account_disabled", "This account is no longer active.", HttpStatusCode.Forbidden);
        }

        if (!user.TwoFactorEnabled || string.IsNullOrWhiteSpace(user.TwoFactorSecret))
        {
            throw new IdentityException("two_factor_not_enabled", "Two-factor authentication is not enabled.", HttpStatusCode.BadRequest);
        }

        var secret = UnprotectTwoFactorSecret(user.TwoFactorSecret);
        if (!VerifyTwoFactorCode(secret, code))
        {
            // Increment failed attempts and update the challenge
            // Note: In high-concurrency scenarios, consider using database-level atomic operations
            // or optimistic concurrency control. The global rate limiter provides additional protection.
            challenge.FailedAttempts++;

            if (challenge.FailedAttempts >= _twoFactorOptions.MaxVerificationAttempts)
            {
                await _twoFactorChallengeRepository.DeleteAsync(challenge, cancellationToken);
                throw new IdentityException("two_factor_attempts_exceeded", "Too many failed attempts. Please log in again.", HttpStatusCode.Unauthorized);
            }

            await _twoFactorChallengeRepository.UpdateAsync(challenge, cancellationToken);
            throw new IdentityException("invalid_two_factor_code", "The authentication code is invalid.", HttpStatusCode.Unauthorized);
        }

        await _twoFactorChallengeRepository.DeleteAsync(challenge, cancellationToken);

        string? rememberDeviceToken = null;
        if (request.RememberDevice)
        {
            rememberDeviceToken = GenerateSecureToken();
            var device = new TwoFactorRememberedDevice
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = HashToken(rememberDeviceToken),
                CreatedAt = now,
                ExpiresAt = now.AddDays(_twoFactorOptions.RememberDeviceDays)
            };

            await _twoFactorRememberedDeviceRepository.AddAsync(device, cancellationToken);
        }

        var token = _tokenService.GenerateToken(user.Id, user.Email);
        return new AuthenticationResult
        {
            UserId = user.Id,
            Email = user.Email,
            Token = token,
            RememberDeviceToken = rememberDeviceToken
        };
    }

    public async Task<TwoFactorSetupResult> BeginTwoFactorSetupAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await GetActiveUserAsync(userId, cancellationToken);

        if (user.TwoFactorEnabled)
        {
            throw new IdentityException("two_factor_already_enabled", "Two-factor authentication is already enabled.", HttpStatusCode.BadRequest);
        }

        var secret = GenerateTwoFactorSecret();
        var now = DateTimeOffset.UtcNow;
        user.TwoFactorPendingSecret = ProtectTwoFactorSecret(secret);
        user.TwoFactorPendingSecretExpiresAt = now.AddMinutes(_twoFactorOptions.SetupMinutes);
        user.UpdatedAt = now;

        await _userRepository.UpdateAsync(user, cancellationToken);

        return new TwoFactorSetupResult
        {
            Secret = secret,
            OtpauthUri = BuildOtpAuthUri(user.Email, secret),
            ExpiresAt = user.TwoFactorPendingSecretExpiresAt.Value
        };
    }

    public async Task EnableTwoFactorAsync(Guid userId, EnableTwoFactorRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new IdentityException("invalid_request", "Two-factor setup payload is required.", HttpStatusCode.BadRequest);
        }

        var user = await GetActiveUserAsync(userId, cancellationToken);

        if (user.TwoFactorEnabled)
        {
            throw new IdentityException("two_factor_already_enabled", "Two-factor authentication is already enabled.", HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(user.TwoFactorPendingSecret) || !user.TwoFactorPendingSecretExpiresAt.HasValue)
        {
            throw new IdentityException("two_factor_setup_required", "Start two-factor setup first.", HttpStatusCode.BadRequest);
        }

        var now = DateTimeOffset.UtcNow;
        if (user.TwoFactorPendingSecretExpiresAt <= now)
        {
            user.TwoFactorPendingSecret = null;
            user.TwoFactorPendingSecretExpiresAt = null;
            user.UpdatedAt = now;
            await _userRepository.UpdateAsync(user, cancellationToken);
            throw new IdentityException("two_factor_setup_expired", "Two-factor setup expired. Please start again.", HttpStatusCode.BadRequest);
        }

        var code = RequireTwoFactorCode(request.Code, HttpStatusCode.BadRequest);
        var pendingSecret = UnprotectTwoFactorSecret(user.TwoFactorPendingSecret);
        if (!VerifyTwoFactorCode(pendingSecret, code))
        {
            throw new IdentityException("invalid_two_factor_code", "The authentication code is invalid.", HttpStatusCode.BadRequest);
        }

        user.TwoFactorEnabled = true;
        user.TwoFactorSecret = user.TwoFactorPendingSecret;
        user.TwoFactorPendingSecret = null;
        user.TwoFactorPendingSecretExpiresAt = null;
        user.TwoFactorEnabledAt = now;
        user.UpdatedAt = now;

        await _userRepository.UpdateAsync(user, cancellationToken);

        _ = Task.Run(async () =>
        {
            try
            {
                await SendTwoFactorEnabledEmailAsync(user.Email, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send two-factor activation email to {Email}", user.Email);
            }
        }, CancellationToken.None);
    }

    public async Task DisableTwoFactorAsync(Guid userId, DisableTwoFactorRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new IdentityException("invalid_request", "Two-factor disable payload is required.", HttpStatusCode.BadRequest);
        }

        var user = await GetActiveUserAsync(userId, cancellationToken);

        if (!user.TwoFactorEnabled || string.IsNullOrWhiteSpace(user.TwoFactorSecret))
        {
            throw new IdentityException("two_factor_not_enabled", "Two-factor authentication is not enabled.", HttpStatusCode.BadRequest);
        }

        var code = RequireTwoFactorCode(request.Code, HttpStatusCode.BadRequest);
        var secret = UnprotectTwoFactorSecret(user.TwoFactorSecret);
        if (!VerifyTwoFactorCode(secret, code))
        {
            throw new IdentityException("invalid_two_factor_code", "The authentication code is invalid.", HttpStatusCode.BadRequest);
        }

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.TwoFactorEnabledAt = null;
        user.TwoFactorPendingSecret = null;
        user.TwoFactorPendingSecretExpiresAt = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _twoFactorRememberedDeviceRepository.DeleteByUserAsync(user.Id, cancellationToken);
        await _twoFactorChallengeRepository.DeleteByUserAsync(user.Id, cancellationToken);
    }

    public async Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new IdentityException("invalid_token", "Verification token is required.", HttpStatusCode.BadRequest);
        }

        var user = await _userRepository.GetByEmailVerificationTokenAsync(token, cancellationToken);

        if (user is null || user.IsDeleted)
        {
            throw new IdentityException("invalid_token", "Verification token is invalid.", HttpStatusCode.BadRequest);
        }

        if (user.EmailVerificationTokenExpiresAt < DateTimeOffset.UtcNow)
        {
            throw new IdentityException("token_expired", "Verification token has expired.", HttpStatusCode.BadRequest);
        }

        // Security note: When changing email addresses (PendingEmail scenario), we update the primary email
        // upon verification. This means an attacker who gains access to the new email address before the
        // legitimate user verifies it could potentially hijack the account. This risk is mitigated by:
        // 1. Requiring the current email to be verified before allowing changes (see UpdateEmailAsync)
        // 2. Sending verification emails to the NEW address only (not the old one)
        // 3. Time-limited verification tokens (24 hours by default)
        // 4. The user retains access via their old email until verification is complete
        // For high-security applications, consider requiring password re-authentication for email changes.
        if (!string.IsNullOrWhiteSpace(user.PendingEmail))
        {
            user.Email = user.PendingEmail;
            user.PendingEmail = null;
        }

        user.IsEmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiresAt = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userRepository.UpdateAsync(user, cancellationToken);
        return true;
    }

    public async Task ResendVerificationEmailAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await GetActiveUserAsync(userId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        if (user.LastVerificationEmailSentAt.HasValue &&
            now < user.LastVerificationEmailSentAt.Value.Add(VerificationEmailRateLimit))
        {
            var retryAfter = user.LastVerificationEmailSentAt.Value.Add(VerificationEmailRateLimit) - now;
            var waitSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
            throw new IdentityException(
                "rate_limited",
                $"Please wait {waitSeconds} seconds before requesting another verification email.",
                HttpStatusCode.TooManyRequests);
        }

        var verificationToken = GenerateVerificationToken();
        user.EmailVerificationToken = verificationToken;
        user.EmailVerificationTokenExpiresAt = now.AddHours(EmailVerificationTokenExpirationHours);
        user.LastVerificationEmailSentAt = now;
        user.UpdatedAt = now;

        if (user.PendingEmail is not null)
        {
            await _userRepository.UpdateAsync(user, cancellationToken);

            _ = Task.Run(async () =>
            {
                try
                {
                    await SendVerificationEmailAsync(user.PendingEmail, verificationToken, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to resend verification email to pending address {Email}", user.PendingEmail);
                }
            });
            return;
        }

        if (user.IsEmailVerified)
        {
            throw new IdentityException("already_verified", "Email is already verified.", HttpStatusCode.BadRequest);
        }

        await _userRepository.UpdateAsync(user, cancellationToken);

        // Send the verification email (fire and forget to not block response)
        _ = Task.Run(async () =>
        {
            try
            {
                await SendVerificationEmailAsync(user.Email, verificationToken, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resend verification email to {Email}", user.Email);
            }
        }, CancellationToken.None);
    }

    public async Task UpdateEmailAsync(Guid userId, UpdateEmailRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new IdentityException("invalid_request", "Email is required.", HttpStatusCode.BadRequest);
        }

        var user = await GetActiveUserAsync(userId, cancellationToken);
        if (!user.IsEmailVerified)
        {
            throw new IdentityException("email_not_verified", "Please verify your current email before changing it.", HttpStatusCode.BadRequest);
        }
        var email = NormalizeEmail(request.Email);

        if (string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(user.PendingEmail) &&
            string.Equals(user.PendingEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            // If the same address is already pending, just refresh the verification token/expiration.
            var refreshedToken = GenerateVerificationToken();
            user.EmailVerificationToken = refreshedToken;
            user.EmailVerificationTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(EmailVerificationTokenExpirationHours);
            user.LastVerificationEmailSentAt = DateTimeOffset.UtcNow;
            user.UpdatedAt = DateTimeOffset.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);

            _ = Task.Run(async () =>
            {
                try
                {
                    await SendVerificationEmailAsync(email, refreshedToken, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to resend verification email for pending address {Email}", email);
                }
            }, CancellationToken.None);

            return;
        }

        var existing = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (existing is not null && existing.Id != user.Id)
        {
            throw new IdentityException("email_in_use", "An account with this email already exists.", HttpStatusCode.Conflict);
        }

        var now = DateTimeOffset.UtcNow;
        var verificationToken = GenerateVerificationToken();
        user.EmailVerificationToken = verificationToken;
        user.EmailVerificationTokenExpiresAt = now.AddHours(EmailVerificationTokenExpirationHours);
        user.PendingEmail = email;
        user.LastVerificationEmailSentAt = now;
        user.UpdatedAt = now;

        await _userRepository.UpdateAsync(user, cancellationToken);

        // Send verification email for the new email address (fire and forget)
        _ = Task.Run(async () =>
        {
            try
            {
                await SendVerificationEmailAsync(email, verificationToken, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification email for updated address {Email}", email);
            }
        }, CancellationToken.None);
    }

    public async Task UpdatePasswordAsync(Guid userId, UpdatePasswordRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new IdentityException("invalid_request", "Password update payload is required.", HttpStatusCode.BadRequest);
        }

        var user = await GetActiveUserAsync(userId, cancellationToken);

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            throw new IdentityException("password_not_set", "Password-based login is not enabled for this account.", HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new IdentityException("invalid_password", "Current password is required.", HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new IdentityException("invalid_password", "New password is required.", HttpStatusCode.BadRequest);
        }

        if (string.Equals(request.Password, request.NewPassword, StringComparison.Ordinal))
        {
            throw new IdentityException("password_reused", "New password must be different from the current password.", HttpStatusCode.BadRequest);
        }

        if (!VerifyPassword(user.PasswordHash, request.Password))
        {
            throw new IdentityException("invalid_credentials", "Current password is incorrect.", HttpStatusCode.BadRequest);
        }

        ValidatePassword(request.NewPassword);
        user.PasswordHash = HashPassword(request.NewPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userRepository.UpdateAsync(user, cancellationToken);

        _ = Task.Run(async () =>
        {
            try
            {
                await SendPasswordChangedEmailAsync(user.Email, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password change notification to {Email}", user.Email);
            }
        }, CancellationToken.None);
    }

    public async Task DeleteAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
                   ?? throw new IdentityException("user_not_found", "User not found.", HttpStatusCode.NotFound);

        if (user.IsDeleted)
        {
            return;
        }

        user.IsDeleted = true;
        user.PasswordHash = null;
        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.TwoFactorPendingSecret = null;
        user.TwoFactorPendingSecretExpiresAt = null;
        user.TwoFactorEnabledAt = null;
        user.StripeCustomerId = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var apiKey in user.ApiKeys)
        {
            apiKey.IsActive = false;
        }

        await _userRepository.UpdateAsync(user, cancellationToken);
    }

    private async Task<User> GetActiveUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
                   ?? throw new IdentityException("user_not_found", "User not found.", HttpStatusCode.NotFound);

        if (user.IsDeleted)
        {
            throw new IdentityException("account_disabled", "This account is no longer active.", HttpStatusCode.Forbidden);
        }

        return user;
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new IdentityException("invalid_email", "Email is required.", HttpStatusCode.BadRequest);
        }

        try
        {
            var mailAddress = new MailAddress(email.Trim());
            return mailAddress.Address.ToLowerInvariant();
        }
        catch (FormatException)
        {
            throw new IdentityException("invalid_email", "Email format is invalid.", HttpStatusCode.BadRequest);
        }
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new IdentityException("weak_password", "Password must be at least 8 characters long.", HttpStatusCode.BadRequest);
        }
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var derived = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(derived)}";
    }

    private static bool VerifyPassword(string hash, string password)
    {
        var parts = hash.Split('.', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var stored = Convert.FromBase64String(parts[1]);
            var attempt = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, stored.Length);
            return CryptographicOperations.FixedTimeEquals(stored, attempt);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string RequireTwoFactorCode(string code, HttpStatusCode statusCode)
    {
        var normalized = NormalizeTwoFactorCode(code);
        if (normalized.Length != 6)
        {
            throw new IdentityException("invalid_two_factor_code", "Enter the 6-digit authentication code.", statusCode);
        }

        return normalized;
    }

    private static string NormalizeTwoFactorCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        return new string(code.Where(char.IsDigit).ToArray());
    }

    private static string GenerateSecureToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string GenerateTwoFactorSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    private string ProtectTwoFactorSecret(string secret)
    {
        return _twoFactorProtector.Protect(secret);
    }

    private string UnprotectTwoFactorSecret(string secret)
    {
        try
        {
            return _twoFactorProtector.Unprotect(secret);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt two-factor secret.");
            throw new IdentityException(
                "two_factor_unavailable",
                "Two-factor authentication needs to be reset. Please contact support.",
                HttpStatusCode.Conflict);
        }
    }

    private bool VerifyTwoFactorCode(string secret, string code)
    {
        try
        {
            var bytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(bytes);
            return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            _logger.LogError(ex, "Failed to decode two-factor secret.");
            throw new IdentityException(
                "two_factor_unavailable",
                "Two-factor authentication needs to be reset. Please contact support.",
                HttpStatusCode.Conflict);
        }
    }

    private string BuildOtpAuthUri(string email, string secret)
    {
        var issuer = _twoFactorOptions.Issuer;
        var label = $"{issuer}:{email}";
        var encodedLabel = Uri.EscapeDataString(label);
        var encodedIssuer = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{encodedLabel}?secret={secret}&issuer={encodedIssuer}";
    }

    private static string GenerateVerificationToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static bool IsEmailVerificationRequired(User user)
    {
        if (user.IsEmailVerified)
        {
            return false;
        }

        var thirtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-EmailVerificationRequiredDays);
        return user.CreatedAt <= thirtyDaysAgo;
    }

    private async Task SendVerificationEmailAsync(string email, string verificationToken, CancellationToken cancellationToken)
    {
        var verificationUrl = $"https://paperapi.de/verify-email?token={Uri.EscapeDataString(verificationToken)}";

        var emailContent = $@"
<h2>Verify your email</h2>
<p style=""color: #f8fafc;"">Please confirm your email address to activate your account or confirm the requested change:</p>
<p style=""text-align: center; margin: 20px 0;"">
    <a href=""{verificationUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #10b981; color: white; text-decoration: none; border-radius: 8px; font-weight: bold;"">Verify Email</a>
</p>
<p style=""font-size: 12px; color: rgba(248, 250, 252, 0.6); margin-top: 20px;"">
    This link will expire in 24 hours. If you did not request this action, you can safely ignore this email.
</p>
";

        await _emailService.SendEmailAsync(
            to: email,
            subject: "Verify Your Email - PaperAPI",
            body: emailContent,
            isHtml: true,
            cancellationToken: cancellationToken
        );
    }

    private async Task SendWelcomeEmailAsync(string email, CancellationToken cancellationToken)
    {
        var emailContent = @"
<h2>Welcome to PaperAPI!</h2>
<p>Your account has been created successfully.</p>
<p>You can now start using our EU-hosted HTML to PDF API. Check out our documentation to learn more:</p>
<ul>
    <li>Generate PDFs with a simple HTTP request</li>
    <li>EU-hosted and GDPR compliant</li>
    <li>Flexible pricing plans for all needs</li>
</ul>
<p>If you have any questions, feel free to reach out to our support team.</p>
";

        await _emailService.SendEmailAsync(
            to: email,
            subject: "Welcome to PaperAPI",
            body: emailContent,
            isHtml: true,
            cancellationToken: cancellationToken
        );
    }

    private async Task SendTwoFactorEnabledEmailAsync(string email, CancellationToken cancellationToken)
    {
        var emailContent = @"
<h2>Two-factor authentication enabled</h2>
<p style=""color: #f8fafc;"">You have successfully enabled two-factor authentication on your PaperAPI account.</p>
<p style=""color: #f8fafc;"">If you did not perform this action, please contact support immediately and reset your password.</p>
";

        await _emailService.SendEmailAsync(
            to: email,
            subject: "Two-factor authentication enabled",
            body: emailContent,
            isHtml: true,
            cancellationToken: cancellationToken
        );
    }

    private async Task SendPasswordChangedEmailAsync(string email, CancellationToken cancellationToken)
    {
        var emailContent = @"
<h2>Password updated</h2>
<p style=""color: #f8fafc;"">This is a confirmation that the password for your PaperAPI account was changed successfully.</p>
<p style=""color: #f8fafc;"">If you did not perform this change, please reset your password immediately and contact support.</p>
";

        await _emailService.SendEmailAsync(
            to: email,
            subject: "Your PaperAPI password was changed",
            body: emailContent,
            isHtml: true,
            cancellationToken: cancellationToken
        );
    }
}
