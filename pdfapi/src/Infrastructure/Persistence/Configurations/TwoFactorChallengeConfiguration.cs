using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaperAPI.Domain.Identity;

namespace PaperAPI.Infrastructure.Persistence.Configurations;

public sealed class TwoFactorChallengeConfiguration : IEntityTypeConfiguration<TwoFactorChallenge>
{
    public void Configure(EntityTypeBuilder<TwoFactorChallenge> builder)
    {
        builder.ToTable("two_factor_challenges");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(c => c.TokenHash)
            .IsUnique();

        builder.Property(c => c.CreatedAt)
            .HasColumnType("timestamptz");

        builder.Property(c => c.ExpiresAt)
            .HasColumnType("timestamptz");

        builder.Property(c => c.FailedAttempts)
            .HasDefaultValue(0);
    }
}
