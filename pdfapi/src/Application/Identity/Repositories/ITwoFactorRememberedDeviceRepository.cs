using PaperAPI.Domain.Identity;

namespace PaperAPI.Application.Identity.Repositories;

public interface ITwoFactorRememberedDeviceRepository
{
    Task<TwoFactorRememberedDevice?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task AddAsync(TwoFactorRememberedDevice device, CancellationToken cancellationToken);
    Task UpdateAsync(TwoFactorRememberedDevice device, CancellationToken cancellationToken);
    Task DeleteAsync(TwoFactorRememberedDevice device, CancellationToken cancellationToken);
    Task DeleteByUserAsync(Guid userId, CancellationToken cancellationToken);
}
