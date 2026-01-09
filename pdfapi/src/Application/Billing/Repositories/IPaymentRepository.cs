using PaperAPI.Domain.Billing;

namespace PaperAPI.Application.Billing.Repositories;

public interface IPaymentRepository
{
    Task<Payment?> GetByStripeInvoiceIdAsync(string stripeInvoiceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Payment>> GetRecentByUserAsync(Guid userId, int limit, CancellationToken cancellationToken);
    Task AddAsync(Payment payment, CancellationToken cancellationToken);
    Task UpdateAsync(Payment payment, CancellationToken cancellationToken);
}
