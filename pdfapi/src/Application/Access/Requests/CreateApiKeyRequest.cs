namespace PaperAPI.Application.Access.Requests;

public sealed class CreateApiKeyRequest
{
    public string Name { get; init; } = string.Empty;
}
