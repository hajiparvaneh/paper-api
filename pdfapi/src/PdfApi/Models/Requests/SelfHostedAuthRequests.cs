namespace PaperAPI.PdfApi.Models.Requests;

public sealed class SelfHostedSetupRequest
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public sealed class SelfHostedLoginRequest
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
