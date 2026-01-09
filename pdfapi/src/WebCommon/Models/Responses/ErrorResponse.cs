using System.Text.Json.Serialization;

namespace PaperAPI.WebCommon.Models.Responses;

public sealed record ErrorResponse(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("details")] string Details,
    [property: JsonPropertyName("requestId")] string? RequestId = null);
