using System.ComponentModel.DataAnnotations;

namespace PaperAPI.WebCommon.Options;

public sealed class DataProtectionOptions
{
    public const string SectionName = "DataProtection";

    [Required]
    public string KeyPath { get; init; } = "dp-keys";

    [Required]
    public string ApplicationName { get; init; } = "PaperAPI";
}
