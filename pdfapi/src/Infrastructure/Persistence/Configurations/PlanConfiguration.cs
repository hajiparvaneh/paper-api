using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaperAPI.Domain.Billing;

namespace PaperAPI.Infrastructure.Persistence.Configurations;

public sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("plans");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(p => p.Code).IsUnique();

        builder.Property(p => p.PriorityWeight)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(p => p.LogRetentionDays)
            .IsRequired()
            .HasDefaultValue(7);

        builder.Property(p => p.OveragePricePerThousandCents)
            .IsRequired()
            .HasDefaultValue(0);
    }
}
