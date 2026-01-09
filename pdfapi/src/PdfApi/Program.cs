using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PaperAPI.Infrastructure.Persistence;
using PaperAPI.PdfApi.Composition;
using PaperAPI.PdfApi.Endpoints;
using PaperAPI.WebCommon.Middleware;
using Prometheus;
using Serilog;

LoadEnvironmentFromFile();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling =
        JsonNumberHandling.Strict | JsonNumberHandling.AllowReadingFromString;
});

builder.Services.AddPdfApiServices(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseSerilogRequestLogging();
app.UseCors(DependencyInjection.CorsPolicyName);
app.UseHttpMetrics();
app.UseMiddleware<ApiKeyAuthMiddleware>();
app.UseRateLimiter();

GeneratePdfEndpoint.Map(app);
PdfJobEndpoints.Map(app);
UsageEndpoints.Map(app);
UserEndpoints.Map(app);
SelfHostedAuthEndpoints.Map(app);
SelfHostedApiKeysEndpoints.Map(app);
HealthEndpoint.Map(app);
app.MapMetrics();

await ApplyMigrationsAsync(app);

app.Run();

static void LoadEnvironmentFromFile()
{
    var envFile = Environment.GetEnvironmentVariable("ENV_FILE");

    if (!string.IsNullOrWhiteSpace(envFile) && TryLoadEnvFile(envFile))
    {
        return;
    }

    var directory = Directory.GetCurrentDirectory();
    while (!string.IsNullOrEmpty(directory))
    {
        if (TryLoadEnvFile(Path.Combine(directory, ".env")))
        {
            break;
        }

        var parent = Directory.GetParent(directory);
        directory = parent?.FullName ?? string.Empty;
    }
}

static bool TryLoadEnvFile(string candidate)
{
    if (string.IsNullOrWhiteSpace(candidate))
    {
        return false;
    }

    var path = Path.IsPathRooted(candidate)
        ? candidate
        : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), candidate));

    if (!File.Exists(path))
    {
        return false;
    }

    foreach (var rawLine in File.ReadAllLines(path))
    {
        var line = rawLine.Trim();

        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
        {
            continue;
        }

        if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
        {
            line = line["export ".Length..].Trim();
        }

        var separatorIndex = line.IndexOf('=');

        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim();

        if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
        {
            value = value[1..^1];
        }

        if (string.IsNullOrWhiteSpace(key) || Environment.GetEnvironmentVariable(key) is { Length: > 0 })
        {
            continue;
        }

        Environment.SetEnvironmentVariable(key, value);
    }

    return true;
}

static async Task ApplyMigrationsAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var dbContext = scope.ServiceProvider.GetRequiredService<PaperApiDbContext>();
    const int maxAttempts = 10;
    for (var attempt = 1; attempt <= maxAttempts; attempt += 1)
    {
        try
        {
            await dbContext.Database.MigrateAsync();
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(ex, "Database migration failed (attempt {Attempt}/{Max}). Retrying...", attempt, maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(Math.Min(5 * attempt, 30)));
        }
    }

    await dbContext.Database.MigrateAsync();
}

public partial class Program;
