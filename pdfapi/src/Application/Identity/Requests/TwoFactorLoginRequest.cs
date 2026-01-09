namespace PaperAPI.Application.Identity.Requests;

public sealed class TwoFactorLoginRequest
{
    public string Token { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public bool RememberDevice { get; init; }
}
