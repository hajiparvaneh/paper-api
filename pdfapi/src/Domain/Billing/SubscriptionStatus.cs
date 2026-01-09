namespace PaperAPI.Domain.Billing;

public enum SubscriptionStatus
{
    Trial = 0,
    Active = 1,
    PastDue = 2,
    Canceled = 3
}
