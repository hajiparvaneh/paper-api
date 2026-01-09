using PaperAPI.Application.Access.Requests;
using PaperAPI.Application.Access.Responses;

namespace PaperAPI.Application.Access.Services;

public interface IAccessService
{
    Task<CreateApiKeyResult> CreateApiKeyAsync(Guid userId, CreateApiKeyRequest request, CancellationToken cancellationToken);
    Task DeactivateApiKeyAsync(Guid userId, Guid apiKeyId, CancellationToken cancellationToken);
    Task ActivateApiKeyAsync(Guid userId, Guid apiKeyId, CancellationToken cancellationToken);
    Task RemoveApiKeyAsync(Guid userId, Guid apiKeyId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApiKeyDto>> GetApiKeysAsync(Guid userId, CancellationToken cancellationToken);
}
