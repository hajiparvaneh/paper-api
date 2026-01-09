namespace PaperAPI.Application.Identity.Requests;

public sealed class DisableTwoFactorRequest
{
    public string Code { get; init; } = string.Empty;
}
