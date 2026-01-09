using PaperAPI.Domain.Access;
using PaperAPI.Domain.Identity;

namespace PaperAPI.Domain.Pdf;

public sealed class PdfJob
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ApiKeyId { get; set; }
    public string Html { get; set; } = string.Empty;
    public string? PageSize { get; set; }
    public string? Orientation { get; set; }
    public decimal? MarginTop { get; set; }
    public decimal? MarginRight { get; set; }
    public decimal? MarginBottom { get; set; }
    public decimal? MarginLeft { get; set; }
    public bool? PrintMediaType { get; set; }
    public bool? DisableSmartShrinking { get; set; }
    public bool? EnableJavascript { get; set; }
    public bool? DisableJavascript { get; set; }
    public string? HeaderLeft { get; set; }
    public string? HeaderCenter { get; set; }
    public string? HeaderRight { get; set; }
    public string? FooterLeft { get; set; }
    public string? FooterCenter { get; set; }
    public string? FooterRight { get; set; }
    public decimal? HeaderSpacing { get; set; }
    public decimal? FooterSpacing { get; set; }
    public string? HeaderHtml { get; set; }
    public string? FooterHtml { get; set; }
    public int? Dpi { get; set; }
    public double? Zoom { get; set; }
    public int? ImageDpi { get; set; }
    public int? ImageQuality { get; set; }
    public bool? LowQuality { get; set; }
    public bool? Images { get; set; }
    public bool? NoImages { get; set; }
    public PdfJobStatus Status { get; set; } = PdfJobStatus.Queued;
    public int PriorityWeight { get; set; }
    public long InputSizeBytes { get; set; }
    public long OutputSizeBytes { get; set; }
    public int DurationMs { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int RetentionDays { get; set; } = 7;
    public string? OutputPath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? IdempotencyHash { get; set; }
    public DateTimeOffset? IdempotencyKeyExpiresAt { get; set; }

    public User? User { get; set; }
    public ApiKey? ApiKey { get; set; }
}
