using PaperAPI.Domain.Pdf;

namespace PaperAPI.Application.Pdf.Repositories;

public interface IPdfJobRepository
{
    Task AddAsync(PdfJob job, CancellationToken cancellationToken);
    Task<PdfJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<PdfJob?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken);
    Task<PdfJob?> GetByIdempotencyKeyAsync(Guid userId, string key, CancellationToken cancellationToken);
    Task UpdateAsync(PdfJob job, CancellationToken cancellationToken);
    Task ClearIdempotencyKeyAsync(Guid jobId, CancellationToken cancellationToken);
    Task UpdateStatusAsync(
        Guid jobId,
        PdfJobStatus status,
        string? outputPath,
        string? errorMessage,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        long? outputSizeBytes,
        int? durationMs,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<PdfJob>> GetRecentFailuresAsync(Guid userId, int count, CancellationToken cancellationToken);
    Task<IReadOnlyList<PdfJob>> GetRecentForUserAsync(
        Guid userId,
        PdfJobStatus? status,
        DateTimeOffset? createdFrom,
        DateTimeOffset? createdTo,
        int limit,
        CancellationToken cancellationToken);
}
