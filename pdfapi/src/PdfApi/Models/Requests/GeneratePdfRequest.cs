using System.Text.Json.Serialization;
using PaperAPI.Application.Pdf;

namespace PaperAPI.PdfApi.Models.Requests;

public sealed class GeneratePdfRequest
{
    [JsonPropertyName("html")]
    public string? Html { get; init; }

    [JsonPropertyName("options")]
    public PdfOptions? Options { get; init; }
}
