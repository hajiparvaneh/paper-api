namespace PaperAPI.Application.Identity.Requests;

public sealed class UpdatePasswordRequest
{
    public string Password { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}
