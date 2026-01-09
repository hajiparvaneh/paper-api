namespace PaperAPI.Application.Access.Responses;

public sealed class CreateApiKeyResult
{
    public ApiKeyDto Key { get; init; } = default!;
    public string PlaintextKey { get; init; } = string.Empty;
}
