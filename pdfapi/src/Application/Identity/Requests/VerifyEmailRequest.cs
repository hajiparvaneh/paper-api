namespace PaperAPI.Application.Identity.Requests;

public sealed class VerifyEmailRequest
{
    public string Token { get; init; } = string.Empty;
}
