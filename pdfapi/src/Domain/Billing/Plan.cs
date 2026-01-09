namespace PaperAPI.Domain.Billing;

public sealed class Plan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int MonthlyPriceCents { get; set; }
    public int MaxPdfsPerMonth { get; set; }
    public int MaxRequestsPerMinute { get; set; }
    public bool IsActive { get; set; }
    public int PriorityWeight { get; set; } = 1;
    public int LogRetentionDays { get; set; } = 7;
    public int OveragePricePerThousandCents { get; set; }
}
