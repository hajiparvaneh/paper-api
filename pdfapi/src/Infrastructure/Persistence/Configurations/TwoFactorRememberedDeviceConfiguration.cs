using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaperAPI.Domain.Identity;

namespace PaperAPI.Infrastructure.Persistence.Configurations;

public sealed class TwoFactorRememberedDeviceConfiguration : IEntityTypeConfiguration<TwoFactorRememberedDevice>
{
    public void Configure(EntityTypeBuilder<TwoFactorRememberedDevice> builder)
    {
        builder.ToTable("two_factor_remembered_devices");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(d => d.TokenHash)
            .IsUnique();

        builder.Property(d => d.CreatedAt)
            .HasColumnType("timestamptz");

        builder.Property(d => d.ExpiresAt)
            .HasColumnType("timestamptz");

        builder.Property(d => d.LastUsedAt)
            .HasColumnType("timestamptz");
    }
}
