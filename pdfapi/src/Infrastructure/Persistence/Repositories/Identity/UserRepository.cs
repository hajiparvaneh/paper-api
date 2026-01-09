using Microsoft.EntityFrameworkCore;
using PaperAPI.Application.Identity.Repositories;
using PaperAPI.Domain.Identity;

namespace PaperAPI.Infrastructure.Persistence.Repositories.Identity;

public sealed class UserRepository : IUserRepository
{
    private readonly PaperApiDbContext _dbContext;

    public UserRepository(PaperApiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .Include(u => u.ApiKeys)
            .Include(u => u.Subscriptions)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .Include(u => u.ApiKeys)
            .Include(u => u.Subscriptions)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<User?> GetByEmailVerificationTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        // No need to include ApiKeys and Subscriptions for email verification
        return await _dbContext.Users
            .FirstOrDefaultAsync(u => u.EmailVerificationToken == token, cancellationToken);
    }

    public async Task<User?> GetByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stripeCustomerId))
        {
            return null;
        }

        return await _dbContext.Users
            .Include(u => u.ApiKeys)
            .Include(u => u.Subscriptions)
            .FirstOrDefaultAsync(u => u.StripeCustomerId == stripeCustomerId, cancellationToken);
    }

    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken)
    {
        // Note: This method does not include related entities (ApiKeys, Subscriptions) for performance.
        // If you need related entities, add a separate method or modify this based on your use case.
        return await _dbContext.Users
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await _dbContext.Users.AddAsync(user, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
