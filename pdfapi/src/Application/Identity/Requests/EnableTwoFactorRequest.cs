namespace PaperAPI.Application.Identity.Requests;

public sealed class EnableTwoFactorRequest
{
    public string Code { get; init; } = string.Empty;
}
