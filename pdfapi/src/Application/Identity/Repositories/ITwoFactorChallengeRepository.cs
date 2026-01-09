using PaperAPI.Domain.Identity;

namespace PaperAPI.Application.Identity.Repositories;

public interface ITwoFactorChallengeRepository
{
    Task<TwoFactorChallenge?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task AddAsync(TwoFactorChallenge challenge, CancellationToken cancellationToken);
    Task UpdateAsync(TwoFactorChallenge challenge, CancellationToken cancellationToken);
    Task DeleteAsync(TwoFactorChallenge challenge, CancellationToken cancellationToken);
    Task DeleteByUserAsync(Guid userId, CancellationToken cancellationToken);
}
