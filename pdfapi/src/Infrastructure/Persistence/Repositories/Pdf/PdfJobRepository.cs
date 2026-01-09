using Microsoft.EntityFrameworkCore;
using PaperAPI.Application.Pdf.Repositories;
using PaperAPI.Domain.Pdf;

namespace PaperAPI.Infrastructure.Persistence.Repositories.Pdf;

public sealed class PdfJobRepository : IPdfJobRepository
{
    private readonly PaperApiDbContext _dbContext;

    public PdfJobRepository(PaperApiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(PdfJob job, CancellationToken cancellationToken)
    {
        await _dbContext.PdfJobs.AddAsync(job, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PdfJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.PdfJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    public async Task<PdfJob?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.PdfJobs.FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId, cancellationToken);
    }

    public async Task<PdfJob?> GetByIdempotencyKeyAsync(Guid userId, string key, CancellationToken cancellationToken)
    {
        return await _dbContext.PdfJobs.FirstOrDefaultAsync(
            j => j.UserId == userId &&
                 j.IdempotencyKey == key,
            cancellationToken);
    }

    public async Task UpdateAsync(PdfJob job, CancellationToken cancellationToken)
    {
        _dbContext.PdfJobs.Update(job);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearIdempotencyKeyAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _dbContext.PdfJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        job.IdempotencyKey = null;
        job.IdempotencyHash = null;
        job.IdempotencyKeyExpiresAt = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(
        Guid jobId,
        PdfJobStatus status,
        string? outputPath,
        string? errorMessage,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        long? outputSizeBytes,
        int? durationMs,
        CancellationToken cancellationToken)
    {
        var job = await _dbContext.PdfJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        job.Status = status;
        job.ErrorMessage = errorMessage;

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            job.OutputPath = outputPath;
        }

        if (startedAt.HasValue)
        {
            job.StartedAt = startedAt;
        }

        if (completedAt.HasValue)
        {
            job.CompletedAt = completedAt;
        }

        if (outputSizeBytes.HasValue)
        {
            job.OutputSizeBytes = outputSizeBytes.Value;
        }

        if (durationMs.HasValue)
        {
            job.DurationMs = durationMs.Value;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PdfJob>> GetRecentFailuresAsync(Guid userId, int count, CancellationToken cancellationToken)
    {
        return await _dbContext.PdfJobs
            .Where(j => j.UserId == userId && j.Status == PdfJobStatus.Failed)
            .OrderByDescending(j => j.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PdfJob>> GetRecentForUserAsync(
        Guid userId,
        PdfJobStatus? status,
        DateTimeOffset? createdFrom,
        DateTimeOffset? createdTo,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.PdfJobs
            .AsNoTracking()
            .Where(j => j.UserId == userId)
            .Where(j => status.HasValue
                ? j.Status == status
                : j.Status == PdfJobStatus.Succeeded || j.Status == PdfJobStatus.Failed);
        if (createdFrom.HasValue)
        {
            query = query.Where(j => j.CreatedAt >= createdFrom.Value);
        }

        if (createdTo.HasValue)
        {
            query = query.Where(j => j.CreatedAt <= createdTo.Value);
        }

        return await query
            .OrderByDescending(j => j.CreatedAt)
            .Take(limit)
            // Project into lightweight entities so large HTML payloads stay off the wire.
            .Select(j => new PdfJob
            {
                Id = j.Id,
                UserId = j.UserId,
                ApiKeyId = j.ApiKeyId,
                PageSize = j.PageSize,
                Orientation = j.Orientation,
                MarginTop = j.MarginTop,
                MarginRight = j.MarginRight,
                MarginBottom = j.MarginBottom,
                MarginLeft = j.MarginLeft,
                PrintMediaType = j.PrintMediaType,
                DisableSmartShrinking = j.DisableSmartShrinking,
                EnableJavascript = j.EnableJavascript,
                DisableJavascript = j.DisableJavascript,
                HeaderLeft = j.HeaderLeft,
                HeaderCenter = j.HeaderCenter,
                HeaderRight = j.HeaderRight,
                FooterLeft = j.FooterLeft,
                FooterCenter = j.FooterCenter,
                FooterRight = j.FooterRight,
                HeaderSpacing = j.HeaderSpacing,
                FooterSpacing = j.FooterSpacing,
                HeaderHtml = j.HeaderHtml,
                FooterHtml = j.FooterHtml,
                Dpi = j.Dpi,
                Zoom = j.Zoom,
                ImageDpi = j.ImageDpi,
                ImageQuality = j.ImageQuality,
                LowQuality = j.LowQuality,
                Images = j.Images,
                NoImages = j.NoImages,
                Status = j.Status,
                InputSizeBytes = j.InputSizeBytes,
                OutputSizeBytes = j.OutputSizeBytes,
                DurationMs = j.DurationMs,
                CreatedAt = j.CreatedAt,
                StartedAt = j.StartedAt,
                CompletedAt = j.CompletedAt,
                ErrorMessage = j.ErrorMessage,
                RetentionDays = j.RetentionDays,
                ExpiresAt = j.ExpiresAt
            })
            .ToListAsync(cancellationToken);
    }
}
