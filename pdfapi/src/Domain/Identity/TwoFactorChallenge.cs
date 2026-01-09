namespace PaperAPI.Domain.Identity;

public sealed class TwoFactorChallenge
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int FailedAttempts { get; set; }

    public User User { get; set; } = null!;
}
