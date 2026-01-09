using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaperAPI.Domain.Access;

namespace PaperAPI.Infrastructure.Persistence.Configurations;

public sealed class UsageRecordConfiguration : IEntityTypeConfiguration<UsageRecord>
{
    public void Configure(EntityTypeBuilder<UsageRecord> builder)
    {
        builder.ToTable("usage_records");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Date)
            .HasColumnType("date");

        builder.Property(r => r.RequestsCount).HasDefaultValue(0);
        builder.Property(r => r.PdfCount).HasDefaultValue(0);
        builder.Property(r => r.BytesGenerated).HasDefaultValue(0L);

        builder.HasIndex(r => new { r.UserId, r.ApiKeyId, r.Date }).IsUnique();
    }
}
