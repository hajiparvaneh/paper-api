using PaperAPI.Application.Billing.Dtos;
using PaperAPI.Application.Billing.Requests;
using PaperAPI.Domain.Billing;

namespace PaperAPI.Application.Billing.Services;

public interface IBillingService
{
    Task<IReadOnlyList<PlanSummary>> GetActivePlansAsync(CancellationToken cancellationToken);
    Task<SubscriptionSummary> GetSubscriptionSummaryAsync(Guid userId, CancellationToken cancellationToken);
    Task<string> CreateCheckoutSessionAsync(Guid userId, string planCode, BillingInterval interval, CancellationToken cancellationToken);
    Task<string> CreateCustomerPortalSessionAsync(Guid userId, CancellationToken cancellationToken);
    Task<BillingProfileDto> GetBillingProfileAsync(Guid userId, CancellationToken cancellationToken);
    Task UpdateBillingProfileAsync(Guid userId, UpdateBillingProfileRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<InvoiceSummary>> GetInvoicesAsync(Guid userId, CancellationToken cancellationToken);
    Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken cancellationToken);
}
