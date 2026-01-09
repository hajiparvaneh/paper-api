namespace PaperAPI.Application.Identity.Responses;

public sealed class AuthenticationResult
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? Token { get; init; }
    public bool TwoFactorRequired { get; init; }
    public string? TwoFactorToken { get; init; }
    public string? RememberDeviceToken { get; init; }
}
