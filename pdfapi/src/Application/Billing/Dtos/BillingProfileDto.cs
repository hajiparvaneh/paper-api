namespace PaperAPI.Application.Billing.Dtos;

public sealed class BillingProfileDto
{
    public string? CompanyName { get; init; }
    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public string? VatNumber { get; init; }
}
