using Microsoft.EntityFrameworkCore;
using PaperAPI.Application.Identity.Repositories;
using PaperAPI.Domain.Identity;

namespace PaperAPI.Infrastructure.Persistence.Repositories.Identity;

public sealed class TwoFactorRememberedDeviceRepository : ITwoFactorRememberedDeviceRepository
{
    private readonly PaperApiDbContext _dbContext;

    public TwoFactorRememberedDeviceRepository(PaperApiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TwoFactorRememberedDevice?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return null;
        }

        return await _dbContext.TwoFactorRememberedDevices
            .FirstOrDefaultAsync(d => d.TokenHash == tokenHash, cancellationToken);
    }

    public async Task AddAsync(TwoFactorRememberedDevice device, CancellationToken cancellationToken)
    {
        await _dbContext.TwoFactorRememberedDevices.AddAsync(device, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TwoFactorRememberedDevice device, CancellationToken cancellationToken)
    {
        _dbContext.TwoFactorRememberedDevices.Update(device);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(TwoFactorRememberedDevice device, CancellationToken cancellationToken)
    {
        _dbContext.TwoFactorRememberedDevices.Remove(device);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var devices = await _dbContext.TwoFactorRememberedDevices
            .Where(d => d.UserId == userId)
            .ToListAsync(cancellationToken);

        if (devices.Count == 0)
        {
            return;
        }

        _dbContext.TwoFactorRememberedDevices.RemoveRange(devices);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
