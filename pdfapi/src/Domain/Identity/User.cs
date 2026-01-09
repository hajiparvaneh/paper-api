using PaperAPI.Domain.Access;
using PaperAPI.Domain.Billing;
using PaperAPI.Domain.Pdf;

namespace PaperAPI.Domain.Identity;

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public bool IsEmailVerified { get; set; }
    public string? PendingEmail { get; set; }
    public string? EmailVerificationToken { get; set; }
    public DateTimeOffset? EmailVerificationTokenExpiresAt { get; set; }
    public DateTimeOffset? LastVerificationEmailSentAt { get; set; }
    public string? StripeCustomerId { get; set; }
    public string? CompanyName { get; set; }
    public string? BillingAddressLine1 { get; set; }
    public string? BillingAddressLine2 { get; set; }
    public string? BillingCity { get; set; }
    public string? BillingState { get; set; }
    public string? BillingPostalCode { get; set; }
    public string? BillingCountry { get; set; }
    public string? VatNumber { get; set; }
    public DateTimeOffset? TermsAcceptedAtUtc { get; set; }
    public DateTimeOffset? PrivacyAcknowledgedAtUtc { get; set; }
    public DateTimeOffset? DpaAcknowledgedAtUtc { get; set; }
    public string? AcceptedFromIp { get; set; }
    public string? AcceptedUserAgent { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorSecret { get; set; }
    public string? TwoFactorPendingSecret { get; set; }
    public DateTimeOffset? TwoFactorPendingSecretExpiresAt { get; set; }
    public DateTimeOffset? TwoFactorEnabledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public List<ApiKey> ApiKeys { get; } = new();
    public List<TwoFactorChallenge> TwoFactorChallenges { get; } = new();
    public List<TwoFactorRememberedDevice> TwoFactorRememberedDevices { get; } = new();
    public List<Subscription> Subscriptions { get; } = new();
    public List<PdfJob> PdfJobs { get; } = new();
}
