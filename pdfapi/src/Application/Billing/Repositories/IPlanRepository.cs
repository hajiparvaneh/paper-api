using PaperAPI.Domain.Billing;

namespace PaperAPI.Application.Billing.Repositories;

public interface IPlanRepository
{
    Task<IReadOnlyList<Plan>> GetActiveAsync(CancellationToken cancellationToken);
    Task<Plan?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task AddAsync(Plan plan, CancellationToken cancellationToken);
}
