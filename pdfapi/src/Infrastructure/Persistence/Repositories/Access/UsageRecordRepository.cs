using Microsoft.EntityFrameworkCore;
using PaperAPI.Application.Access.Repositories;
using PaperAPI.Domain.Access;

namespace PaperAPI.Infrastructure.Persistence.Repositories.Access;

public sealed class UsageRecordRepository : IUsageRecordRepository
{
    private readonly PaperApiDbContext _dbContext;

    public UsageRecordRepository(PaperApiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UsageRecord?> GetByDateAsync(Guid userId, Guid? apiKeyId, DateOnly date, CancellationToken cancellationToken)
    {
        return await _dbContext.UsageRecords
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ApiKeyId == apiKeyId && r.Date == date, cancellationToken);
    }

    public async Task UpsertAsync(UsageRecord record, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.UsageRecords.FirstOrDefaultAsync(r => r.Id == record.Id, cancellationToken);
        if (existing is null)
        {
            await _dbContext.UsageRecords.AddAsync(record, cancellationToken);
        }
        else
        {
            _dbContext.Entry(existing).CurrentValues.SetValues(record);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task IncrementUsageAsync(Guid userId, Guid? apiKeyId, DateOnly date, int requestCount, int pdfCount, long bytesGenerated, CancellationToken cancellationToken)
    {
        var record = await GetByDateAsync(userId, apiKeyId, date, cancellationToken);
        if (record is null)
        {
            record = new UsageRecord
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ApiKeyId = apiKeyId,
                Date = date,
                RequestsCount = requestCount,
                PdfCount = pdfCount,
                BytesGenerated = bytesGenerated
            };
            await _dbContext.UsageRecords.AddAsync(record, cancellationToken);
        }
        else
        {
            record.RequestsCount += requestCount;
            record.PdfCount += pdfCount;
            record.BytesGenerated += bytesGenerated;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetMonthlyPdfCountAsync(Guid userId, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        return await _dbContext.UsageRecords
            .Where(r => r.UserId == userId && r.Date >= start && r.Date < end)
            .SumAsync(r => r.PdfCount, cancellationToken);
    }
}
