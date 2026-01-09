using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaperAPI.Domain.Billing;

namespace PaperAPI.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.StripeInvoiceId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.Currency)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(p => p.InvoiceDate)
            .HasColumnType("timestamptz");
        builder.Property(p => p.PeriodStart)
            .HasColumnType("timestamptz");
        builder.Property(p => p.PeriodEnd)
            .HasColumnType("timestamptz");
        builder.Property(p => p.Description)
            .HasMaxLength(255);
        builder.Property(p => p.HostedInvoiceUrl)
            .HasMaxLength(1024);
        builder.Property(p => p.InvoicePdfUrl)
            .HasMaxLength(1024);

        builder.HasIndex(p => p.StripeInvoiceId).IsUnique();
    }
}
