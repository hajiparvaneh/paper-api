using Microsoft.EntityFrameworkCore;
using PaperAPI.Application.Billing.Repositories;
using PaperAPI.Domain.Billing;

namespace PaperAPI.Infrastructure.Persistence.Repositories.Billing;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly PaperApiDbContext _dbContext;

    public PaymentRepository(PaperApiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Payment?> GetByStripeInvoiceIdAsync(string stripeInvoiceId, CancellationToken cancellationToken)
    {
        return await _dbContext.Payments.FirstOrDefaultAsync(p => p.StripeInvoiceId == stripeInvoiceId, cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetRecentByUserAsync(Guid userId, int limit, CancellationToken cancellationToken)
    {
        return await _dbContext.Payments
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.InvoiceDate)
            .ThenByDescending(p => p.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        await _dbContext.Payments.AddAsync(payment, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Payment payment, CancellationToken cancellationToken)
    {
        _dbContext.Payments.Update(payment);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
