using System.Net;
using PaperAPI.Application.Access.Repositories;
using PaperAPI.Application.Billing.Repositories;
using PaperAPI.Application.Identity.Exceptions;
using PaperAPI.Application.Identity.Repositories;
using PaperAPI.Application.Identity.Responses;
using PaperAPI.Domain.Identity;

namespace PaperAPI.Application.Identity.Services;

public sealed class UserProfileService : IUserProfileService
{
    private readonly IUserRepository _userRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IUsageRecordRepository _usageRecordRepository;

    public UserProfileService(
        IUserRepository userRepository,
        ISubscriptionRepository subscriptionRepository,
        IUsageRecordRepository usageRecordRepository)
    {
        _userRepository = userRepository;
        _subscriptionRepository = subscriptionRepository;
        _usageRecordRepository = usageRecordRepository;
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
                   ?? throw new IdentityException("user_not_found", "User not found.", HttpStatusCode.NotFound);

        return await BuildProfileAsync(user, cancellationToken);
    }

    public async Task<UserProfileDto> GetProfileByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken)
                   ?? throw new IdentityException("user_not_found", "User not found.", HttpStatusCode.NotFound);

        return await BuildProfileAsync(user, cancellationToken);
    }

    public async Task<UserAgreementsDto> GetAgreementsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
                   ?? throw new IdentityException("user_not_found", "User not found.", HttpStatusCode.NotFound);

        if (user.IsDeleted)
        {
            throw new IdentityException("account_disabled", "This account is no longer active.", HttpStatusCode.Forbidden);
        }

        return new UserAgreementsDto
        {
            TermsAcceptedAtUtc = user.TermsAcceptedAtUtc,
            PrivacyAcknowledgedAtUtc = user.PrivacyAcknowledgedAtUtc,
            DpaAcknowledgedAtUtc = user.DpaAcknowledgedAtUtc,
            AcceptedFromIp = user.AcceptedFromIp,
            AcceptedUserAgent = user.AcceptedUserAgent
        };
    }

    private async Task<UserProfileDto> BuildProfileAsync(User user, CancellationToken cancellationToken)
    {
        if (user.IsDeleted)
        {
            throw new IdentityException("account_disabled", "This account is no longer active.", HttpStatusCode.Forbidden);
        }

        var subscription = await _subscriptionRepository.GetActiveByUserAsync(user.Id, cancellationToken);
        var plan = BuildPlanSummary(subscription);
        var usageCount = await GetCurrentMonthUsageAsync(user.Id, cancellationToken);

        plan = plan with
        {
            Limits = new UsageSummaryDto
            {
                MonthlyLimit = plan.Limits.MonthlyLimit,
                UsedThisMonth = usageCount
            }
        };

        var usage = new UsageSummaryDto
        {
            UsedThisMonth = usageCount,
            MonthlyLimit = plan.Limits.MonthlyLimit
        };

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            IsEmailVerified = user.IsEmailVerified,
            PendingEmail = user.PendingEmail,
            TwoFactorEnabled = user.TwoFactorEnabled,
            TermsAcceptedAtUtc = user.TermsAcceptedAtUtc,
            PrivacyAcknowledgedAtUtc = user.PrivacyAcknowledgedAtUtc,
            DpaAcknowledgedAtUtc = user.DpaAcknowledgedAtUtc,
            AcceptedFromIp = user.AcceptedFromIp,
            AcceptedUserAgent = user.AcceptedUserAgent,
            CreatedAt = user.CreatedAt,
            Plan = plan,
            Usage = usage
        };
    }

    private static UserPlanSummaryDto BuildPlanSummary(Domain.Billing.Subscription? subscription)
    {
        if (subscription?.Plan is null)
        {
            return CloneDefaultPlan();
        }

        var plan = subscription.Plan;
        var priceEuros = plan.MonthlyPriceCents / 100m;

        return new UserPlanSummaryDto
        {
            Name = plan.Name,
            Price = $"€{priceEuros:0.##}",
            Limits = new UsageSummaryDto
            {
                MonthlyLimit = plan.MaxPdfsPerMonth,
                UsedThisMonth = 0
            }
        };
    }

    private async Task<int> GetCurrentMonthUsageAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var start = new DateOnly(now.Year, now.Month, 1);
        var endExclusive = start.AddMonths(1);
        return await _usageRecordRepository.GetMonthlyPdfCountAsync(userId, start, endExclusive, cancellationToken);
    }

    private static UserPlanSummaryDto CloneDefaultPlan()
    {
        return new UserPlanSummaryDto
        {
            Name = "Free",
            Price = "€0",
            Limits = new UsageSummaryDto
            {
                MonthlyLimit = 50,
                UsedThisMonth = 0
            }
        };
    }
}
