using System.Linq;
using PaperAPI.Application.Identity.Options;
using PaperAPI.Infrastructure.Options;
using PaperAPI.WebCommon.Options;

namespace PaperAPI.PdfApi.Composition;

public static class OptionsSetup
{
    public static IServiceCollection AddAppOptions(this IServiceCollection services, IConfiguration configuration)
    {
        var appSection = configuration.GetSection(AppOptions.SectionName);
        var enablePdfProcessing = configuration.GetValue<bool?>("App:PdfProcessing:Enabled") ?? true;

        services.AddOptions<AppOptions>()
            .Bind(appSection)
            .ValidateDataAnnotations()
            .Validate(options => options.ApiKeys.Any(), "At least one API key is required.")
            .ValidateOnStart();

        services.AddOptions<AuthOptions>()
            .Bind(appSection.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => !string.IsNullOrWhiteSpace(options.JwtSecret) && options.JwtSecret.Length >= 32, "Auth:JwtSecret must be at least 32 characters long.")
            .ValidateOnStart();

        services.AddOptions<TwoFactorOptions>()
            .Bind(appSection.GetSection(TwoFactorOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<DataProtectionOptions>()
            .Bind(appSection.GetSection(DataProtectionOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<WkhtmlToPdfOptions>()
            .Bind(configuration.GetSection(WkhtmlToPdfOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (!enablePdfProcessing)
        {
            services.AddOptions<StripeOptions>()
                .Bind(configuration.GetSection(StripeOptions.SectionName))
                .ValidateDataAnnotations()
                .Validate(options => !string.IsNullOrWhiteSpace(options.SecretKey), "Stripe:SecretKey is required.")
                .Validate(options => options.PriceIds.Count > 0, "Stripe:PriceIds must contain at least one plan mapping.")
                .Validate(options => options.PriceIds.Values.All(p => p is not null && !string.IsNullOrWhiteSpace(p.Monthly)), "Stripe:PriceIds entries must include a Monthly price ID.")
                .ValidateOnStart();
        }

        return services;
    }
}
