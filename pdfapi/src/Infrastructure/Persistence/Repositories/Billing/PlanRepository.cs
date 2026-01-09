using Microsoft.EntityFrameworkCore;
using PaperAPI.Application.Billing.Repositories;
using PaperAPI.Domain.Billing;

namespace PaperAPI.Infrastructure.Persistence.Repositories.Billing;

public sealed class PlanRepository : IPlanRepository
{
    private readonly PaperApiDbContext _dbContext;

    public PlanRepository(PaperApiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Plan>> GetActiveAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Plans
            .Where(p => p.IsActive)
            .OrderBy(p => p.MonthlyPriceCents)
            .ToListAsync(cancellationToken);
    }

    public async Task<Plan?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return await _dbContext.Plans.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
    }

    public async Task AddAsync(Plan plan, CancellationToken cancellationToken)
    {
        await _dbContext.Plans.AddAsync(plan, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
