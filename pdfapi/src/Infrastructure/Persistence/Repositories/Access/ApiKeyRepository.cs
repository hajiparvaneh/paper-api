using Microsoft.EntityFrameworkCore;
using PaperAPI.Application.Access.Repositories;
using PaperAPI.Domain.Access;

namespace PaperAPI.Infrastructure.Persistence.Repositories.Access;

public sealed class ApiKeyRepository : IApiKeyRepository
{
    private readonly PaperApiDbContext _dbContext;

    public ApiKeyRepository(PaperApiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, cancellationToken);
    }

    public async Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken cancellationToken)
    {
        return await _dbContext.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == keyHash, cancellationToken);
    }

    public async Task<IReadOnlyList<ApiKey>> GetByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.ApiKeys
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken)
    {
        await _dbContext.ApiKeys.AddAsync(apiKey, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApiKey apiKey, CancellationToken cancellationToken)
    {
        _dbContext.ApiKeys.Update(apiKey);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ApiKey apiKey, CancellationToken cancellationToken)
    {
        _dbContext.ApiKeys.Remove(apiKey);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
