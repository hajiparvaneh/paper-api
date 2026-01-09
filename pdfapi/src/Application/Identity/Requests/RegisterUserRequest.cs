namespace PaperAPI.Application.Identity.Requests;

public sealed class RegisterUserRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? Name { get; init; }
    public bool AcceptLegal { get; init; } = true;
    public string? UserAgent { get; init; }
    public string? AcceptanceIp { get; init; }
}
