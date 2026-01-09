using System.Text.Json.Serialization;

namespace PaperAPI.Application.Pdf;

public sealed class PdfOptions
{
    [JsonPropertyName("pageSize")]
    public string? PageSize { get; init; }

    [JsonPropertyName("orientation")]
    public string? Orientation { get; init; }

    [JsonPropertyName("marginTop")]
    public decimal? MarginTop { get; init; }

    [JsonPropertyName("marginRight")]
    public decimal? MarginRight { get; init; }

    [JsonPropertyName("marginBottom")]
    public decimal? MarginBottom { get; init; }

    [JsonPropertyName("marginLeft")]
    public decimal? MarginLeft { get; init; }

    [JsonPropertyName("printMediaType")]
    public bool? PrintMediaType { get; init; }

    [JsonPropertyName("disableSmartShrinking")]
    public bool? DisableSmartShrinking { get; init; }

    [JsonPropertyName("enableJavascript")]
    public bool? EnableJavascript { get; init; }

    [JsonPropertyName("disableJavascript")]
    public bool? DisableJavascript { get; init; }

    [JsonPropertyName("headerLeft")]
    public string? HeaderLeft { get; init; }

    [JsonPropertyName("headerCenter")]
    public string? HeaderCenter { get; init; }

    [JsonPropertyName("headerRight")]
    public string? HeaderRight { get; init; }

    [JsonPropertyName("footerLeft")]
    public string? FooterLeft { get; init; }

    [JsonPropertyName("footerCenter")]
    public string? FooterCenter { get; init; }

    [JsonPropertyName("footerRight")]
    public string? FooterRight { get; init; }

    [JsonPropertyName("headerSpacing")]
    public decimal? HeaderSpacing { get; init; }

    [JsonPropertyName("footerSpacing")]
    public decimal? FooterSpacing { get; init; }

    [JsonPropertyName("headerHtml")]
    public string? HeaderHtml { get; init; }

    [JsonPropertyName("footerHtml")]
    public string? FooterHtml { get; init; }

    [JsonPropertyName("dpi")]
    public int? Dpi { get; init; }

    [JsonPropertyName("zoom")]
    public double? Zoom { get; init; }

    [JsonPropertyName("imageDpi")]
    public int? ImageDpi { get; init; }

    [JsonPropertyName("imageQuality")]
    public int? ImageQuality { get; init; }

    [JsonPropertyName("lowQuality")]
    public bool? LowQuality { get; init; }

    [JsonPropertyName("images")]
    public bool? Images { get; init; }

    [JsonPropertyName("noImages")]
    public bool? NoImages { get; init; }
}
