namespace PaperAPI.Application.Identity.Requests;

public sealed class UpdateEmailRequest
{
    public string Email { get; init; } = string.Empty;
}
