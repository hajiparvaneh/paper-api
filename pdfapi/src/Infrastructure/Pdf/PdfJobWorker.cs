using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaperAPI.Application.Access.Repositories;
using PaperAPI.Application.Pdf;
using PaperAPI.Application.Pdf.Repositories;
using PaperAPI.Domain.Pdf;

namespace PaperAPI.Infrastructure.Pdf;

public sealed class PdfJobWorker : BackgroundService
{
    private readonly IPdfJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PdfJobWorker> _logger;
    private readonly IJobWaiterRegistry _waiterRegistry;

    public PdfJobWorker(
        IPdfJobQueue queue,
        IServiceScopeFactory scopeFactory,
        IJobWaiterRegistry waiterRegistry,
        ILogger<PdfJobWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _waiterRegistry = waiterRegistry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PdfJobWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            PdfJob? job = null;

            try
            {
                job = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dequeue PDF job");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                continue;
            }

            if (job is null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
                continue;
            }

            await ProcessJobAsync(job, stoppingToken);
        }

        _logger.LogInformation("PdfJobWorker stopping");
    }

    private async Task ProcessJobAsync(PdfJob job, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IPdfRenderer>();
        var jobRepository = scope.ServiceProvider.GetRequiredService<IPdfJobRepository>();
        var usageRepository = scope.ServiceProvider.GetRequiredService<IUsageRecordRepository>();

        var startedAt = DateTimeOffset.UtcNow;
        await jobRepository.UpdateStatusAsync(
            job.Id,
            PdfJobStatus.Processing,
            outputPath: null,
            errorMessage: null,
            startedAt,
            completedAt: null,
            outputSizeBytes: null,
            durationMs: null,
            ct);

        var outputPath = BuildOutputPath(job.Id);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            await using (var outputStream = new FileStream(
                         outputPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 81920,
                         options: FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var options = new PdfOptions
                {
                    PageSize = job.PageSize,
                    Orientation = job.Orientation,
                    MarginTop = job.MarginTop,
                    MarginRight = job.MarginRight,
                    MarginBottom = job.MarginBottom,
                    MarginLeft = job.MarginLeft,
                    PrintMediaType = job.PrintMediaType,
                    DisableSmartShrinking = job.DisableSmartShrinking,
                    EnableJavascript = job.EnableJavascript,
                    DisableJavascript = job.DisableJavascript,
                    HeaderLeft = job.HeaderLeft,
                    HeaderCenter = job.HeaderCenter,
                    HeaderRight = job.HeaderRight,
                    FooterLeft = job.FooterLeft,
                    FooterCenter = job.FooterCenter,
                    FooterRight = job.FooterRight,
                    HeaderSpacing = job.HeaderSpacing,
                    FooterSpacing = job.FooterSpacing,
                    HeaderHtml = job.HeaderHtml,
                    FooterHtml = job.FooterHtml,
                    Dpi = job.Dpi,
                    Zoom = job.Zoom,
                    ImageDpi = job.ImageDpi,
                    ImageQuality = job.ImageQuality,
                    LowQuality = job.LowQuality,
                    Images = job.Images,
                    NoImages = job.NoImages
                };

                await renderer.RenderAsync(job.Html, options, outputStream, ct);
                await outputStream.FlushAsync(ct);
            }

            var completedAt = DateTimeOffset.UtcNow;
            var outputSizeBytes = new FileInfo(outputPath).Length;
            var durationMs = (int)Math.Max(0, (completedAt - startedAt).TotalMilliseconds);

            await jobRepository.UpdateStatusAsync(
                job.Id,
                PdfJobStatus.Succeeded,
                outputPath,
                errorMessage: null,
                startedAt,
                completedAt,
                outputSizeBytes,
                durationMs,
                ct);

            await usageRepository.IncrementUsageAsync(
                job.UserId,
                job.ApiKeyId,
                DateOnly.FromDateTime(startedAt.DateTime),
                requestCount: 1,
                pdfCount: 1,
                bytesGenerated: outputSizeBytes,
                ct);

            job.Status = PdfJobStatus.Succeeded;
            job.OutputPath = outputPath;
            job.StartedAt = startedAt;
            job.CompletedAt = completedAt;
            job.OutputSizeBytes = outputSizeBytes;
            job.DurationMs = durationMs;
            _waiterRegistry.NotifyCompleted(job);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process PDF job {JobId}", job.Id);
            var completedAt = DateTimeOffset.UtcNow;

            await jobRepository.UpdateStatusAsync(
                job.Id,
                PdfJobStatus.Failed,
                outputPath: null,
                errorMessage: ex.Message,
                startedAt,
                completedAt,
                outputSizeBytes: null,
                durationMs: null,
                ct);

            TryDeleteFile(outputPath);

            job.Status = PdfJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.StartedAt = startedAt;
            job.CompletedAt = completedAt;
            _waiterRegistry.NotifyCompleted(job);
        }
    }

    private static string BuildOutputPath(Guid jobId)
    {
        var root = Path.Combine(Path.GetTempPath(), "paperapi-jobs");
        return Path.Combine(root, $"{jobId}.pdf");
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete PDF output {Path}", path);
        }
    }
}
