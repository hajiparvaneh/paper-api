using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaperAPI.Domain.Billing;

namespace PaperAPI.Infrastructure.Persistence.Configurations;

public sealed class StripeWebhookEventConfiguration : IEntityTypeConfiguration<StripeWebhookEvent>
{
    public void Configure(EntityTypeBuilder<StripeWebhookEvent> builder)
    {
        builder.ToTable("stripe_webhook_events");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.StripeEventId)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(e => e.StripeEventId).IsUnique();

        builder.Property(e => e.Type)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.PayloadJson)
            .IsRequired();

        builder.Property(e => e.ProcessedAt).HasColumnType("timestamptz");
        builder.Property(e => e.CreatedAt).HasColumnType("timestamptz");
    }
}
