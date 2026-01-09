using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaperAPI.Domain.Billing;

namespace PaperAPI.Infrastructure.Persistence.Configurations;

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.StripeSubscriptionId)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(s => s.StripeSubscriptionId).IsUnique();

        builder.Property(s => s.Interval)
            .HasConversion<int>();

        builder.Property(s => s.CurrentPeriodStart).HasColumnType("timestamptz");
        builder.Property(s => s.CurrentPeriodEnd).HasColumnType("timestamptz");
        builder.Property(s => s.CancelledAt).HasColumnType("timestamptz");
        builder.Property(s => s.CreatedAt).HasColumnType("timestamptz");

        builder.Property(s => s.StripeOverageSubscriptionItemId)
            .HasMaxLength(255);

        builder.Property(s => s.LastOveragePeriodEnd)
            .HasColumnType("timestamptz");

        builder.Property(s => s.LastOverageQuantity)
            .HasDefaultValue(0);
    }
}
