using PaperAPI.Application.Identity.Requests;
using PaperAPI.Application.Identity.Responses;

namespace PaperAPI.Application.Identity.Services;

public interface IIdentityService
{
    Task<AuthenticationResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken);
    Task<AuthenticationResult> LoginAsync(LoginRequest request, string? rememberDeviceToken, CancellationToken cancellationToken);
    Task<AuthenticationResult> VerifyTwoFactorLoginAsync(TwoFactorLoginRequest request, CancellationToken cancellationToken);
    Task<TwoFactorSetupResult> BeginTwoFactorSetupAsync(Guid userId, CancellationToken cancellationToken);
    Task EnableTwoFactorAsync(Guid userId, EnableTwoFactorRequest request, CancellationToken cancellationToken);
    Task DisableTwoFactorAsync(Guid userId, DisableTwoFactorRequest request, CancellationToken cancellationToken);
    Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken);
    Task ResendVerificationEmailAsync(Guid userId, CancellationToken cancellationToken);
    Task UpdateEmailAsync(Guid userId, UpdateEmailRequest request, CancellationToken cancellationToken);
    Task UpdatePasswordAsync(Guid userId, UpdatePasswordRequest request, CancellationToken cancellationToken);
    Task DeleteAccountAsync(Guid userId, CancellationToken cancellationToken);
}
