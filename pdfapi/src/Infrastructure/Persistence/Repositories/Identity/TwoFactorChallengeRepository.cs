using Microsoft.EntityFrameworkCore;
using PaperAPI.Application.Identity.Repositories;
using PaperAPI.Domain.Identity;

namespace PaperAPI.Infrastructure.Persistence.Repositories.Identity;

public sealed class TwoFactorChallengeRepository : ITwoFactorChallengeRepository
{
    private readonly PaperApiDbContext _dbContext;

    public TwoFactorChallengeRepository(PaperApiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TwoFactorChallenge?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return null;
        }

        return await _dbContext.TwoFactorChallenges
            .FirstOrDefaultAsync(c => c.TokenHash == tokenHash, cancellationToken);
    }

    public async Task AddAsync(TwoFactorChallenge challenge, CancellationToken cancellationToken)
    {
        await _dbContext.TwoFactorChallenges.AddAsync(challenge, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TwoFactorChallenge challenge, CancellationToken cancellationToken)
    {
        _dbContext.TwoFactorChallenges.Update(challenge);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(TwoFactorChallenge challenge, CancellationToken cancellationToken)
    {
        _dbContext.TwoFactorChallenges.Remove(challenge);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var challenges = await _dbContext.TwoFactorChallenges
            .Where(c => c.UserId == userId)
            .ToListAsync(cancellationToken);

        if (challenges.Count == 0)
        {
            return;
        }

        _dbContext.TwoFactorChallenges.RemoveRange(challenges);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
