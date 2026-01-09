using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaperAPI.Application.Access.Repositories;
using PaperAPI.Application.Billing.Repositories;
using PaperAPI.Application.Billing.Services;
using PaperAPI.Application.Email;
using PaperAPI.Application.Identity.Repositories;
using PaperAPI.Application.Pdf;
using PaperAPI.Application.Pdf.Repositories;
using PaperAPI.Infrastructure.Billing;
using PaperAPI.Infrastructure.Options;
using PaperAPI.Infrastructure.Pdf;
using PaperAPI.Infrastructure.Persistence;
using PaperAPI.Infrastructure.Persistence.Repositories.Access;
using PaperAPI.Infrastructure.Persistence.Repositories.Billing;
using PaperAPI.Infrastructure.Persistence.Repositories.Identity;
using PaperAPI.Infrastructure.Persistence.Repositories.Pdf;
using PaperAPI.Infrastructure.Services;

namespace PaperAPI.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default") ??
                               "Host=localhost;Port=5432;Database=paperapi;Username=paperapi;Password=paperapi";

        services.AddDbContext<PaperApiDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(PaperApiDbContext).Assembly.FullName);
            });
        });

        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITwoFactorChallengeRepository, TwoFactorChallengeRepository>();
        services.AddScoped<ITwoFactorRememberedDeviceRepository, TwoFactorRememberedDeviceRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IUsageRecordRepository, UsageRecordRepository>();
        services.AddScoped<IPlanRepository, PlanRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IStripeWebhookEventRepository, StripeWebhookEventRepository>();
        services.AddScoped<IBillingService, StripeBillingService>();
        services.AddScoped<IPdfJobRepository, PdfJobRepository>();

        // Configure Email options from configuration
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.AddScoped<IEmailService, EmailService>();

        var enablePdfProcessing = configuration.GetValue<bool?>("App:PdfProcessing:Enabled") ?? true;
        if (enablePdfProcessing)
        {
            services.AddScoped<IPdfRenderer, WkhtmlToPdfRenderer>();
            services.AddSingleton<IJobWaiterRegistry, JobWaiterRegistry>();
            services.AddSingleton<IPdfJobQueue, PriorityPdfJobQueue>();
            services.AddHostedService<PdfJobWorker>();
            services.AddHostedService<PdfJobRetentionService>();
        }

        return services;
    }
}
