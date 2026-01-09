using PaperAPI.Domain.Identity;

namespace PaperAPI.Domain.Billing;

public sealed class Subscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public string StripeSubscriptionId { get; set; } = string.Empty;
    public SubscriptionStatus Status { get; set; }
    public BillingInterval Interval { get; set; } = BillingInterval.Monthly;
    public DateTimeOffset CurrentPeriodStart { get; set; }
    public DateTimeOffset CurrentPeriodEnd { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? StripeOverageSubscriptionItemId { get; set; }
    public DateTimeOffset? LastOveragePeriodEnd { get; set; }
    public int LastOverageQuantity { get; set; }

    public User? User { get; set; }
    public Plan? Plan { get; set; }
}
