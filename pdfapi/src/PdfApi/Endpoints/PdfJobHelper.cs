using Microsoft.AspNetCore.Http;
using PaperAPI.Application.Access.Repositories;
using PaperAPI.Application.Billing.Repositories;
using PaperAPI.Application.Identity.Tokens;
using PaperAPI.WebCommon.Auth;
using PaperAPI.WebCommon.Options;

namespace PaperAPI.PdfApi.Endpoints;

internal static class PdfJobHelper
{
    private const int DefaultPriorityWeight = 1;
    internal const int DefaultMonthlyLimit = 50;
    private const int DefaultLogRetentionDays = 7;

    internal sealed record PlanContext(int PriorityWeight, int MonthlyLimit, int LogRetentionDays);

    public static bool TryGetUserId(HttpContext httpContext, AuthOptions options, ITokenService tokenService, out Guid userId)
    {
        if (httpContext.Items.TryGetValue("UserId", out var storedUserId) && storedUserId is Guid cached)
        {
            userId = cached;
            return true;
        }

        return SessionTokenHelper.TryGetUserId(httpContext, options, tokenService, out userId);
    }

    public static async Task<PlanContext> GetPlanContextAsync(
        ISubscriptionRepository subscriptionRepository,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.GetActiveByUserAsync(userId, cancellationToken);
        if (subscription?.Plan is null)
        {
            return new PlanContext(DefaultPriorityWeight, DefaultMonthlyLimit, DefaultLogRetentionDays);
        }

        var plan = subscription.Plan;
        var weight = Math.Max(plan.PriorityWeight, DefaultPriorityWeight);
        var monthlyLimit = plan.MaxPdfsPerMonth > 0 ? plan.MaxPdfsPerMonth : DefaultMonthlyLimit;
        var retention = plan.LogRetentionDays > 0 ? plan.LogRetentionDays : DefaultLogRetentionDays;
        return new PlanContext(weight, monthlyLimit, retention);
    }

    public static async Task<int> GetMonthlyUsageAsync(
        IUsageRecordRepository usageRecordRepository,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var start = new DateOnly(now.Year, now.Month, 1);
        var endExclusive = start.AddMonths(1);
        return await usageRecordRepository.GetMonthlyPdfCountAsync(userId, start, endExclusive, cancellationToken);
    }

    public static Guid? TryGetApiKeyId(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue("ApiKeyId", out var value) && value is Guid apiKeyId)
        {
            return apiKeyId;
        }

        return null;
    }
}
