using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PaperAPI.Domain.Identity;
using PaperAPI.Infrastructure.Persistence;

namespace PaperAPI.PdfApi.SelfHosted;

public sealed class SelfHostedAdminService
{
    public const string AdminMarker = "selfhosted-admin";
    private readonly PaperApiDbContext _dbContext;

    public SelfHostedAdminService(PaperApiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetAdminAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(user => user.StripeCustomerId == AdminMarker, cancellationToken);
    }

    public async Task<User> CreateAdminAsync(string username, string password, CancellationToken cancellationToken)
    {
        var existing = await GetAdminAsync(cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException("Admin user is already configured.");
        }

        var normalizedUsername = (username ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            throw new ArgumentException("Username is required.", nameof(username));
        }

        if (normalizedUsername.Length > 320)
        {
            throw new ArgumentException("Username must be 320 characters or fewer.", nameof(username));
        }

        ValidatePassword(password);

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedUsername,
            PasswordHash = HashPassword(password),
            IsEmailVerified = true,
            StripeCustomerId = AdminMarker,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<User?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken)
    {
        var admin = await GetAdminAsync(cancellationToken);
        if (admin is null || string.IsNullOrWhiteSpace(admin.PasswordHash))
        {
            return null;
        }

        if (!string.Equals(admin.Email, username?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return VerifyPassword(admin.PasswordHash, password) ? admin : null;
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new ArgumentException("Password must be at least 8 characters long.", nameof(password));
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
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

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
}
