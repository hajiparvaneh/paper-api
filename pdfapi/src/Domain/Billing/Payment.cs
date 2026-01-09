namespace PaperAPI.Domain.Billing;

public sealed class Payment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string StripeInvoiceId { get; set; } = string.Empty;
    public long AmountCents { get; set; }
    public string Currency { get; set; } = "usd";
    public PaymentStatus Status { get; set; }
    public DateTimeOffset InvoiceDate { get; set; }
    public DateTimeOffset? PeriodStart { get; set; }
    public DateTimeOffset? PeriodEnd { get; set; }
    public string? Description { get; set; }
    public string? HostedInvoiceUrl { get; set; }
    public string? InvoicePdfUrl { get; set; }
}
