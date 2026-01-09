namespace PaperAPI.Application.Identity.Responses;

public sealed class UserAgreementsDto
{
    public DateTimeOffset? TermsAcceptedAtUtc { get; init; }
    public DateTimeOffset? PrivacyAcknowledgedAtUtc { get; init; }
    public DateTimeOffset? DpaAcknowledgedAtUtc { get; init; }
    public string? AcceptedFromIp { get; init; }
    public string? AcceptedUserAgent { get; init; }
}
