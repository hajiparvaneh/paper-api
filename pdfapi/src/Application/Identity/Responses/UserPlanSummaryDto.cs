namespace PaperAPI.Application.Identity.Responses;

public sealed record UserPlanSummaryDto
{
    public string Name { get; init; } = string.Empty;
    public string Price { get; init; } = string.Empty;
    public UsageSummaryDto Limits { get; init; } = new();
}
