namespace PaperAPI.Domain.Identity;

public sealed class TwoFactorRememberedDevice
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }

    public User User { get; set; } = null!;
}
