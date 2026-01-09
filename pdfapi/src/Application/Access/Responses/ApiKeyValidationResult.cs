namespace PaperAPI.Application.Access.Responses;

public sealed class ApiKeyValidationResult
{
    public Guid ApiKeyId { get; init; }
    public Guid UserId { get; init; }
}
