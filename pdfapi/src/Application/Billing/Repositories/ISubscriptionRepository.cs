using PaperAPI.Domain.Billing;

namespace PaperAPI.Application.Billing.Repositories;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<Subscription?> GetByStripeIdAsync(string stripeSubscriptionId, CancellationToken cancellationToken);
    Task AddAsync(Subscription subscription, CancellationToken cancellationToken);
    Task UpdateAsync(Subscription subscription, CancellationToken cancellationToken);
}
