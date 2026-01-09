using Microsoft.EntityFrameworkCore;
using PaperAPI.Application.Billing.Repositories;
using PaperAPI.Domain.Billing;

namespace PaperAPI.Infrastructure.Persistence.Repositories.Billing;

public sealed class SubscriptionRepository : ISubscriptionRepository
{
    private readonly PaperApiDbContext _dbContext;

    public SubscriptionRepository(PaperApiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Subscription?> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.UserId == userId && s.Status != SubscriptionStatus.Canceled)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Subscription?> GetByStripeIdAsync(string stripeSubscriptionId, CancellationToken cancellationToken)
    {
        return await _dbContext.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId, cancellationToken);
    }

    public async Task AddAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        await _dbContext.Subscriptions.AddAsync(subscription, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        _dbContext.Subscriptions.Update(subscription);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
