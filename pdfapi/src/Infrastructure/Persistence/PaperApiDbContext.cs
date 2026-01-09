using Microsoft.EntityFrameworkCore;
using PaperAPI.Domain.Access;
using PaperAPI.Domain.Billing;
using PaperAPI.Domain.Identity;
using PaperAPI.Domain.Pdf;

namespace PaperAPI.Infrastructure.Persistence;

public sealed class PaperApiDbContext : DbContext
{
    public PaperApiDbContext(DbContextOptions<PaperApiDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<TwoFactorChallenge> TwoFactorChallenges => Set<TwoFactorChallenge>();
    public DbSet<TwoFactorRememberedDevice> TwoFactorRememberedDevices => Set<TwoFactorRememberedDevice>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<StripeWebhookEvent> StripeWebhookEvents => Set<StripeWebhookEvent>();
    public DbSet<PdfJob> PdfJobs => Set<PdfJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaperApiDbContext).Assembly);
    }
}
