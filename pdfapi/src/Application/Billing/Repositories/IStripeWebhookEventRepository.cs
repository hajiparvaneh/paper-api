using PaperAPI.Domain.Billing;

namespace PaperAPI.Application.Billing.Repositories;

public interface IStripeWebhookEventRepository
{
    Task<bool> ExistsAsync(string stripeEventId, CancellationToken cancellationToken);
    Task AddAsync(StripeWebhookEvent webhookEvent, CancellationToken cancellationToken);
}
