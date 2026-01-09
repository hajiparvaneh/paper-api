using Microsoft.EntityFrameworkCore;
using PaperAPI.Application.Billing.Repositories;
using PaperAPI.Domain.Billing;

namespace PaperAPI.Infrastructure.Persistence.Repositories.Billing;

public sealed class StripeWebhookEventRepository : IStripeWebhookEventRepository
{
    private readonly PaperApiDbContext _dbContext;

    public StripeWebhookEventRepository(PaperApiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> ExistsAsync(string stripeEventId, CancellationToken cancellationToken)
    {
        return await _dbContext.StripeWebhookEvents.AnyAsync(e => e.StripeEventId == stripeEventId, cancellationToken);
    }

    public async Task AddAsync(StripeWebhookEvent webhookEvent, CancellationToken cancellationToken)
    {
        await _dbContext.StripeWebhookEvents.AddAsync(webhookEvent, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
