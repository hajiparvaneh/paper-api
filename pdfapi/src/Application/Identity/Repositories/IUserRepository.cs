using PaperAPI.Domain.Identity;

namespace PaperAPI.Application.Identity.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    Task<User?> GetByEmailVerificationTokenAsync(string token, CancellationToken cancellationToken);
    Task<User?> GetByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken cancellationToken);
    Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken);
    Task AddAsync(User user, CancellationToken cancellationToken);
    Task UpdateAsync(User user, CancellationToken cancellationToken);
}
