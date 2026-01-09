using System.ComponentModel.DataAnnotations;
using PaperAPI.Infrastructure.Options;

namespace PaperAPI.WebCommon.Options;

public sealed class AppOptions
{
    public const string SectionName = "App";

    [Required]
    [MinLength(1)]
    public List<string> ApiKeys { get; init; } = new();

    [Required]
    public WkhtmlToPdfOptions WkhtmlToPdf { get; init; } = new();

    public RateLimitingOptions RateLimiting { get; init; } = new();

    public List<string> AllowedOrigins { get; init; } = new();
}

public sealed class RateLimitingOptions
{
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; init; } = 60;

    [Range(1, int.MaxValue)]
    public int WindowSeconds { get; init; } = 60;

    [Range(0, int.MaxValue)]
    public int QueueLimit { get; init; } = 0;
}
