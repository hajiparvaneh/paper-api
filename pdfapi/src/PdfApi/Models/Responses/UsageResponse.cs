using System.Text.Json.Serialization;

namespace PaperAPI.PdfApi.Models.Responses;

public sealed class UsageResponse
{
    [JsonPropertyName("used")]
    public int Used { get; init; }

    [JsonPropertyName("monthlyLimit")]
    public int MonthlyLimit { get; init; }

    [JsonPropertyName("remaining")]
    public int Remaining { get; init; }

    [JsonPropertyName("overage")]
    public int Overage { get; init; }

    [JsonPropertyName("nextRechargeAt")]
    public DateTimeOffset NextRechargeAt { get; init; }
}
