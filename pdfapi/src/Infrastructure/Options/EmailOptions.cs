namespace PaperAPI.Infrastructure.Options;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = "PaperAPI";
    public bool UseStartTls { get; set; } = true;
    public bool UseSsl { get; set; } = false;
    public bool SkipCertificateValidation { get; set; } = false;
}
