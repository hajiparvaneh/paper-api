using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PaperAPI.Application.Access.Repositories;
using PaperAPI.Application.Billing.Repositories;
using PaperAPI.Application.Identity.Tokens;
using PaperAPI.PdfApi.Models.Responses;
using PaperAPI.WebCommon.Options;

namespace PaperAPI.PdfApi.Endpoints;

public static class UsageEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/usage", GetAsync)
            .WithName("GetUsageSummary");
    }

    private static async Task<IResult> GetAsync(
        HttpContext httpContext,
        IOptions<AuthOptions> authOptions,
        ITokenService tokenService,
        ISubscriptionRepository subscriptionRepository,
        IUsageRecordRepository usageRecordRepository,
        CancellationToken cancellationToken)
    {
        if (!PdfJobHelper.TryGetUserId(httpContext, authOptions.Value, tokenService, out var userId))
        {
            return EndpointHelpers.UnauthorizedResult(httpContext);
        }

        var planContext = await PdfJobHelper.GetPlanContextAsync(subscriptionRepository, userId, cancellationToken);
        var usedThisMonth = await PdfJobHelper.GetMonthlyUsageAsync(usageRecordRepository, userId, cancellationToken);
        var nextRechargeAt = ResolveNextRechargeAt();

        var response = new UsageResponse
        {
            Used = usedThisMonth,
            MonthlyLimit = planContext.MonthlyLimit,
            Remaining = Math.Max(0, planContext.MonthlyLimit - usedThisMonth),
            Overage = Math.Max(0, usedThisMonth - planContext.MonthlyLimit),
            NextRechargeAt = nextRechargeAt
        };

        return Results.Ok(response);
    }

    private static DateTimeOffset ResolveNextRechargeAt()
    {
        var now = DateTimeOffset.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthStart = startOfMonth.AddMonths(1);
        return new DateTimeOffset(nextMonthStart, TimeSpan.Zero);
    }
}
