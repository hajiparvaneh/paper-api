namespace PaperAPI.Application.Identity.Responses;

public sealed record UsageSummaryDto
{
    public int UsedThisMonth { get; init; }
    public int MonthlyLimit { get; init; }
}
