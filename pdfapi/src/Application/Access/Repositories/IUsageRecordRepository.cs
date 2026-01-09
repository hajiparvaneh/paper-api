using PaperAPI.Domain.Access;

namespace PaperAPI.Application.Access.Repositories;

public interface IUsageRecordRepository
{
    Task<UsageRecord?> GetByDateAsync(Guid userId, Guid? apiKeyId, DateOnly date, CancellationToken cancellationToken);
    Task UpsertAsync(UsageRecord record, CancellationToken cancellationToken);
    Task IncrementUsageAsync(Guid userId, Guid? apiKeyId, DateOnly date, int requestCount, int pdfCount, long bytesGenerated, CancellationToken cancellationToken);
    Task<int> GetMonthlyPdfCountAsync(Guid userId, DateOnly start, DateOnly end, CancellationToken cancellationToken);
}
