namespace PaperAPI.Application.Identity.Responses;

public sealed class UserProfileDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool IsEmailVerified { get; init; }
    public string? PendingEmail { get; init; }
    public bool TwoFactorEnabled { get; init; }
    public DateTimeOffset? TermsAcceptedAtUtc { get; init; }
    public DateTimeOffset? PrivacyAcknowledgedAtUtc { get; init; }
    public DateTimeOffset? DpaAcknowledgedAtUtc { get; init; }
    public string? AcceptedFromIp { get; init; }
    public string? AcceptedUserAgent { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public UserPlanSummaryDto Plan { get; init; } = new();
    public UsageSummaryDto Usage { get; init; } = new();
}
