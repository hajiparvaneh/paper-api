using System.ComponentModel.DataAnnotations;

namespace PaperAPI.Application.Identity.Options;

public sealed class TwoFactorOptions
{
    public const string SectionName = "TwoFactor";

    [Required]
    public string Issuer { get; init; } = "PaperAPI";

    [Range(1, 60)]
    public int ChallengeMinutes { get; init; } = 10;

    [Range(1, 60)]
    public int SetupMinutes { get; init; } = 15;

    [Range(1, 365)]
    public int RememberDeviceDays { get; init; } = 30;

    [Required]
    public string RememberDeviceCookieName { get; init; } = "paperapi_2fa";

    [Range(1, 20)]
    public int MaxVerificationAttempts { get; init; } = 5;
}
