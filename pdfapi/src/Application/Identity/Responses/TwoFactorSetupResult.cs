namespace PaperAPI.Application.Identity.Responses;

public sealed class TwoFactorSetupResult
{
    public string Secret { get; init; } = string.Empty;
    public string OtpauthUri { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
}
