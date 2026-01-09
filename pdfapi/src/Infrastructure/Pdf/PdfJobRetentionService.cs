using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaperAPI.Infrastructure.Persistence;

namespace PaperAPI.Infrastructure.Pdf;

public sealed class PdfJobRetentionService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(6);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PdfJobRetentionService> _logger;

    public PdfJobRetentionService(IServiceScopeFactory scopeFactory, ILogger<PdfJobRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay to avoid hammering DB on startup.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while trimming expired PDF logs.");
            }

            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaperApiDbContext>();
        var now = DateTimeOffset.UtcNow;

        var expiredJobs = await dbContext.PdfJobs
            .Where(job => job.ExpiresAt <= now)
            .Select(job => new { job.Id, job.OutputPath })
            .ToListAsync(cancellationToken);

        if (expiredJobs.Count == 0)
        {
            return;
        }

        // Delete physical files first
        var filesDeleted = 0;
        foreach (var job in expiredJobs)
        {
            if (!string.IsNullOrWhiteSpace(job.OutputPath) && File.Exists(job.OutputPath))
            {
                try
                {
                    File.Delete(job.OutputPath);
                    filesDeleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete PDF file {OutputPath} for job {JobId}", job.OutputPath, job.Id);
                }
            }
        }

        // Delete database records using the IDs from the first query
        var jobIds = expiredJobs.Select(j => j.Id).ToList();
        var deleted = await dbContext.PdfJobs
            .Where(job => jobIds.Contains(job.Id))
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            _logger.LogInformation("Removed {Count} expired PDF logs and {FilesDeleted} physical files.", deleted, filesDeleted);
        }
    }
}
