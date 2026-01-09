using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PaperAPI.WebCommon.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    [Required]
    [MinLength(32)]
    public string JwtSecret { get; init; } = "change-me-to-a-secure-random-string-at-least-32-chars";

    [Required]
    public string Issuer { get; init; } = "PaperAPI";

    [Required]
    public string Audience { get; init; } = "PaperAPI";

    [Range(5, 60 * 24 * 365)]
    public int TokenLifetimeMinutes { get; init; } = 60 * 24 * 30;

    [Required]
    public string CookieName { get; init; } = "paperapi_session";

    public string? CookieDomain { get; init; }

    public string CookiePath { get; init; } = "/";

    public bool CookieSecure { get; init; }

    public bool CookieHttpOnly { get; init; } = true;

    public SameSiteMode CookieSameSite { get; init; } = SameSiteMode.Lax;
}
