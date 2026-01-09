namespace PaperAPI.Application.Billing.Dtos;

public sealed class PlanSummary
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public int MonthlyPriceCents { get; init; }
    public int AnnualPriceCents { get; init; }
    public int MaxPdfsPerMonth { get; init; }
    public int MaxRequestsPerMinute { get; init; }
    public int PriorityWeight { get; init; }
    public int LogRetentionDays { get; init; }
    public int OveragePricePerThousandCents { get; init; }
}
