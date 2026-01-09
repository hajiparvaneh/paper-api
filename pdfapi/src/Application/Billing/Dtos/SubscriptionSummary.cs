using PaperAPI.Domain.Billing;

namespace PaperAPI.Application.Billing.Dtos;

public sealed class SubscriptionSummary
{
    public string PlanName { get; init; } = "Free";
    public string Price { get; init; } = "€0";
    public DateTimeOffset? NextBillingDate { get; init; }
    public string Status { get; init; } = "none";
    public BillingInterval Interval { get; init; } = BillingInterval.Monthly;
}
