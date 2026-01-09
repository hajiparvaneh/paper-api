namespace PaperAPI.WebCommon.Options;

public sealed class AdminBasicAuthOptions
{
    public const string SectionName = "AdminBasicAuth";

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password);
}
