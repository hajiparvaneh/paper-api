using System.Text.Json.Serialization;

namespace PaperAPI.PdfApi.Models.Responses;

public sealed class SelfHostedStatusResponse
{
    [JsonPropertyName("isConfigured")]
    public bool IsConfigured { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }
}

public sealed class SelfHostedMeResponse
{
    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;
}
