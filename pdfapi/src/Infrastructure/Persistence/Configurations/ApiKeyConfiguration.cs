using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaperAPI.Domain.Access;

namespace PaperAPI.Infrastructure.Persistence.Configurations;

public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");
        builder.HasKey(k => k.Id);

        builder.Property(k => k.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(k => k.KeyHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(k => k.Prefix)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(k => k.CreatedAt)
            .HasColumnType("timestamptz");

        builder.Property(k => k.LastUsedAt)
            .HasColumnType("timestamptz");

        builder.HasIndex(k => new { k.UserId, k.Name }).IsUnique();
    }
}
