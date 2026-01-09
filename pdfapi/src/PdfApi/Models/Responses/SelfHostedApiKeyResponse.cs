using System.Text.Json.Serialization;

namespace PaperAPI.PdfApi.Models.Responses;

public sealed class SelfHostedApiKeyResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("prefix")]
    public string Prefix { get; init; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("lastUsedAt")]
    public DateTimeOffset? LastUsedAt { get; init; }
}

public sealed class SelfHostedCreateApiKeyResponse
{
    [JsonPropertyName("key")]
    public SelfHostedApiKeyResponse Key { get; init; } = default!;

    [JsonPropertyName("plaintextKey")]
    public string PlaintextKey { get; init; } = string.Empty;
}
