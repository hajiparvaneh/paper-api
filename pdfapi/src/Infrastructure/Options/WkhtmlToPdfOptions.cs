using System.ComponentModel.DataAnnotations;

namespace PaperAPI.Infrastructure.Options;

public sealed class WkhtmlToPdfOptions
{
    public const string SectionName = "App:WkhtmlToPdf";

    [Required]
    public string ExecutablePath { get; init; } = "/usr/bin/wkhtmltopdf";
}
