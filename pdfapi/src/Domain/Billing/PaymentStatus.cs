namespace PaperAPI.Domain.Billing;

public enum PaymentStatus
{
    Unknown = 0,
    Open = 1,
    Paid = 2,
    Void = 3,
    Uncollectible = 4
}
