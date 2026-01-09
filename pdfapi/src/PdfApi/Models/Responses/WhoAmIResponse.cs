using System.Text.Json.Serialization;

namespace PaperAPI.PdfApi.Models.Responses;

public sealed class WhoAmIResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("plan")]
    public WhoAmIPlanResponse Plan { get; init; } = new();
}

public sealed class WhoAmIPlanResponse
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("interval")]
    public string Interval { get; init; } = "monthly";

    [JsonPropertyName("monthlyLimit")]
    public int MonthlyLimit { get; init; }

    [JsonPropertyName("priceCents")]
    public int PriceCents { get; init; }
}
