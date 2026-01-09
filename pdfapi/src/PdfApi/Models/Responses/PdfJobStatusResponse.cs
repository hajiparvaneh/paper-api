using System;
using System.Text.Json.Serialization;

namespace PaperAPI.PdfApi.Models.Responses;

public sealed class PdfJobStatusResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; init; }

    [JsonPropertyName("jobStatusUrl")]
    public string? JobStatusUrl { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset ExpiresAt { get; init; }

    [JsonPropertyName("links")]
    public PdfJobLinks Links { get; init; } = new();
}

public sealed class PdfJobLinks
{
    [JsonPropertyName("self")]
    public string Self { get; init; } = string.Empty;

    [JsonPropertyName("result")]
    public string? Result { get; init; }
}
