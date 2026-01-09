using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaperAPI.Domain.Identity;

namespace PaperAPI.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(320);

        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.PendingEmail)
            .HasMaxLength(320);

        builder.Property(u => u.EmailVerificationToken)
            .HasMaxLength(255);

        builder.HasIndex(u => u.EmailVerificationToken);
        builder.Property(u => u.LastVerificationEmailSentAt)
            .HasColumnType("timestamptz");

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(256);

        builder.Property(u => u.StripeCustomerId)
            .HasMaxLength(255);
        builder.Property(u => u.CompanyName)
            .HasMaxLength(200);
        builder.Property(u => u.BillingAddressLine1)
            .HasMaxLength(200);
        builder.Property(u => u.BillingAddressLine2)
            .HasMaxLength(200);
        builder.Property(u => u.BillingCity)
            .HasMaxLength(120);
        builder.Property(u => u.BillingState)
            .HasMaxLength(120);
        builder.Property(u => u.BillingPostalCode)
            .HasMaxLength(32);
        builder.Property(u => u.BillingCountry)
            .HasMaxLength(2);
        builder.Property(u => u.VatNumber)
            .HasMaxLength(32);
        builder.Property(u => u.AcceptedFromIp)
            .HasMaxLength(45);
        builder.Property(u => u.AcceptedUserAgent)
            .HasMaxLength(2048);

        builder.Property(u => u.TwoFactorSecret)
            .HasMaxLength(512);
        builder.Property(u => u.TwoFactorPendingSecret)
            .HasMaxLength(512);
        builder.Property(u => u.TwoFactorPendingSecretExpiresAt)
            .HasColumnType("timestamptz");
        builder.Property(u => u.TwoFactorEnabledAt)
            .HasColumnType("timestamptz");

        builder.Property(u => u.TermsAcceptedAtUtc)
            .HasColumnType("timestamptz");
        builder.Property(u => u.PrivacyAcknowledgedAtUtc)
            .HasColumnType("timestamptz");
        builder.Property(u => u.DpaAcknowledgedAtUtc)
            .HasColumnType("timestamptz");

        builder.Property(u => u.CreatedAt)
            .HasColumnType("timestamptz");

        builder.Property(u => u.UpdatedAt)
            .HasColumnType("timestamptz");

        builder.HasMany(u => u.ApiKeys)
            .WithOne(k => k.User)
            .HasForeignKey(k => k.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.TwoFactorChallenges)
            .WithOne(c => c.User)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.TwoFactorRememberedDevices)
            .WithOne(d => d.User)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.Subscriptions)
            .WithOne(s => s.User)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.PdfJobs)
            .WithOne(j => j.User)
            .HasForeignKey(j => j.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
