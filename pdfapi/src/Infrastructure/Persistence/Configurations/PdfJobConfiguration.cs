using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaperAPI.Domain.Pdf;

namespace PaperAPI.Infrastructure.Persistence.Configurations;

public sealed class PdfJobConfiguration : IEntityTypeConfiguration<PdfJob>
{
    public void Configure(EntityTypeBuilder<PdfJob> builder)
    {
        builder.ToTable("pdf_jobs");
        builder.HasKey(j => j.Id);

        builder.Property(j => j.Html)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(j => j.PageSize)
            .HasMaxLength(32);

        builder.Property(j => j.Orientation)
            .HasMaxLength(16);

        builder.Property(j => j.MarginTop)
            .HasPrecision(9, 2);

        builder.Property(j => j.MarginRight)
            .HasPrecision(9, 2);

        builder.Property(j => j.MarginBottom)
            .HasPrecision(9, 2);

        builder.Property(j => j.MarginLeft)
            .HasPrecision(9, 2);

        builder.Property(j => j.PrintMediaType);

        builder.Property(j => j.DisableSmartShrinking);

        builder.Property(j => j.EnableJavascript);

        builder.Property(j => j.DisableJavascript);

        builder.Property(j => j.HeaderLeft)
            .HasMaxLength(256);

        builder.Property(j => j.HeaderCenter)
            .HasMaxLength(256);

        builder.Property(j => j.HeaderRight)
            .HasMaxLength(256);

        builder.Property(j => j.FooterLeft)
            .HasMaxLength(256);

        builder.Property(j => j.FooterCenter)
            .HasMaxLength(256);

        builder.Property(j => j.FooterRight)
            .HasMaxLength(256);

        builder.Property(j => j.HeaderSpacing)
            .HasPrecision(9, 2);

        builder.Property(j => j.FooterSpacing)
            .HasPrecision(9, 2);

        builder.Property(j => j.HeaderHtml)
            .HasColumnType("text");

        builder.Property(j => j.FooterHtml)
            .HasColumnType("text");

        builder.Property(j => j.Dpi);

        builder.Property(j => j.Zoom)
            .HasPrecision(9, 2);

        builder.Property(j => j.ImageDpi);

        builder.Property(j => j.ImageQuality);

        builder.Property(j => j.LowQuality);

        builder.Property(j => j.Images);

        builder.Property(j => j.NoImages);

        builder.Property(j => j.PriorityWeight)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(j => j.CreatedAt)
            .HasColumnType("timestamptz");

        builder.Property(j => j.StartedAt)
            .HasColumnType("timestamptz");

        builder.Property(j => j.CompletedAt)
            .HasColumnType("timestamptz");

        builder.Property(j => j.ExpiresAt)
            .HasColumnType("timestamptz");

        builder.Property(j => j.OutputPath)
            .HasMaxLength(512);

        builder.Property(j => j.InputSizeBytes)
            .HasDefaultValue(0L);

        builder.Property(j => j.OutputSizeBytes)
            .HasDefaultValue(0L);

        builder.Property(j => j.DurationMs)
            .HasDefaultValue(0);

        builder.Property(j => j.RetentionDays)
            .HasDefaultValue(7);

        builder.Property(j => j.ErrorMessage)
            .HasMaxLength(1024);

        builder.Property(j => j.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(128);

        builder.Property(j => j.IdempotencyHash)
            .HasColumnName("idempotency_hash")
            .HasMaxLength(128);

        builder.Property(j => j.IdempotencyKeyExpiresAt)
            .HasColumnName("idempotency_key_expires_at")
            .HasColumnType("timestamptz");

        builder.HasIndex(j => new { j.UserId, j.CreatedAt });
        builder.HasIndex(j => new { j.UserId, j.IdempotencyKey })
            .HasDatabaseName("IX_pdf_jobs_UserId_IdempotencyKey")
            .HasFilter("\"idempotency_key\" IS NOT NULL");
    }
}
