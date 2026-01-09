using System.IO;
using System.Linq;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.DataProtection;
using PaperAPI.Application.DependencyInjection;
using PaperAPI.Application.Identity.Tokens;
using PaperAPI.Infrastructure.DependencyInjection;
using PaperAPI.PdfApi.Models.Requests;
using PaperAPI.PdfApi.SelfHosted;
using PaperAPI.WebCommon.Options;
using PaperAPI.WebCommon.Services;
using DataProtectionOptions = PaperAPI.WebCommon.Options.DataProtectionOptions;

namespace PaperAPI.PdfApi.Composition;

public static class DependencyInjection
{
    public const string CorsPolicyName = "AppCorsPolicy";

    public static IServiceCollection AddPdfApiServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddAppOptions(configuration);
        services.AddApplicationServices();
        services.AddInfrastructureServices(configuration);
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<SelfHostedAdminService>();

        var dataProtectionOptions = configuration.GetSection(AppOptions.SectionName)
            .GetSection(DataProtectionOptions.SectionName)
            .Get<DataProtectionOptions>() ?? new DataProtectionOptions();

        var dataProtectionBuilder = services.AddDataProtection()
            .SetApplicationName(dataProtectionOptions.ApplicationName);

        if (!string.IsNullOrWhiteSpace(dataProtectionOptions.KeyPath))
        {
            var resolvedKeyPath = Path.IsPathRooted(dataProtectionOptions.KeyPath)
                ? dataProtectionOptions.KeyPath
                : Path.Combine(environment.ContentRootPath, dataProtectionOptions.KeyPath);

            Directory.CreateDirectory(resolvedKeyPath);
            dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(resolvedKeyPath));
        }

        var appOptions = configuration.GetSection(AppOptions.SectionName).Get<AppOptions>() ?? new AppOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = appOptions.RateLimiting.PermitLimit,
                    Window = TimeSpan.FromSeconds(appOptions.RateLimiting.WindowSeconds),
                    QueueLimit = appOptions.RateLimiting.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
            });
        });

        services.AddHealthChecks();
        services.AddProblemDetails();

        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<GeneratePdfRequestValidator>();

        var allowedOriginsList = appOptions.AllowedOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (environment.IsDevelopment())
        {
            AddOriginIfMissing("http://localhost:3000");
            AddOriginIfMissing("https://localhost:3000");
            AddOriginIfMissing("http://localhost:3001");
            AddOriginIfMissing("https://localhost:3001");
        }

        var allowedOrigins = allowedOriginsList.ToArray();

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                if (allowedOrigins.Length == 0)
                {
                    policy
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()
                        .SetIsOriginAllowed(_ => true);
                }
                else
                {
                    policy
                        .WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
            });
        });

        return services;

        void AddOriginIfMissing(string origin)
        {
            if (!allowedOriginsList.Contains(origin, StringComparer.OrdinalIgnoreCase))
            {
                allowedOriginsList.Add(origin);
            }
        }
    }
}
