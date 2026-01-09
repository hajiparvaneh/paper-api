using System.ComponentModel.DataAnnotations;

namespace PaperAPI.Infrastructure.Options;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    [Required]
    public string PublishableKey { get; init; } = string.Empty;

    [Required]
    public string SecretKey { get; init; } = string.Empty;

    [Required]
    public string WebhookSecret { get; init; } = string.Empty;

    [Required]
    [Url]
    public string SuccessUrl { get; init; } = string.Empty;

    [Required]
    [Url]
    public string CancelUrl { get; init; } = string.Empty;

    [Required]
    [Url]
    public string PortalReturnUrl { get; init; } = string.Empty;

    public Dictionary<string, StripePlanPriceOptions> PriceIds { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class StripePlanPriceOptions
{
    public string Monthly { get; init; } = string.Empty;
    public string Annual { get; init; } = string.Empty;
    public string Overage { get; init; } = string.Empty;
}
