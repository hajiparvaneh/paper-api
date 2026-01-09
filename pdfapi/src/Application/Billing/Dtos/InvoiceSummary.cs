using PaperAPI.Domain.Billing;

namespace PaperAPI.Application.Billing.Dtos;

public sealed class InvoiceSummary
{
    public Guid PaymentId { get; init; }
    public string StripeInvoiceId { get; init; } = string.Empty;
    public long AmountCents { get; init; }
    public string Currency { get; init; } = "usd";
    public PaymentStatus Status { get; init; } = PaymentStatus.Unknown;
    public DateTimeOffset InvoiceDate { get; init; }
    public DateTimeOffset? PeriodStart { get; init; }
    public DateTimeOffset? PeriodEnd { get; init; }
    public string? Description { get; init; }
    public string? HostedInvoiceUrl { get; init; }
    public string? InvoicePdfUrl { get; init; }
}
