using PaperAPI.Domain.Access;

namespace PaperAPI.Application.Access.Repositories;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApiKey>> GetByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken);
    Task UpdateAsync(ApiKey apiKey, CancellationToken cancellationToken);
    Task DeleteAsync(ApiKey apiKey, CancellationToken cancellationToken);
}
