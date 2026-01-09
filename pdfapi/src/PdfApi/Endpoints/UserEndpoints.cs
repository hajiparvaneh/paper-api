using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PaperAPI.Application.Billing.Repositories;
using PaperAPI.Application.Identity.Repositories;
using PaperAPI.Application.Identity.Tokens;
using PaperAPI.Domain.Billing;
using PaperAPI.PdfApi.Models.Responses;
using PaperAPI.WebCommon.Models.Responses;
using PaperAPI.WebCommon.Options;

namespace PaperAPI.PdfApi.Endpoints;

public static class UserEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/whoami", GetWhoAmIAsync)
            .WithName("WhoAmI");
    }

    private static async Task<IResult> GetWhoAmIAsync(
        HttpContext httpContext,
        IOptions<AuthOptions> authOptions,
        ITokenService tokenService,
        IUserRepository userRepository,
        ISubscriptionRepository subscriptionRepository,
        CancellationToken cancellationToken)
    {
        if (!PdfJobHelper.TryGetUserId(httpContext, authOptions.Value, tokenService, out var userId))
        {
            return EndpointHelpers.UnauthorizedResult(httpContext);
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.IsDeleted)
        {
            return Results.Json(new ErrorResponse("user_not_found", "User profile is not accessible.", httpContext.TraceIdentifier), statusCode: StatusCodes.Status404NotFound);
        }

        var subscription = await subscriptionRepository.GetActiveByUserAsync(userId, cancellationToken);
        var response = new WhoAmIResponse
        {
            Id = user.Id,
            Email = user.Email,
            Name = ResolveDisplayName(user.CompanyName, user.Email),
            Plan = BuildPlanResponse(subscription)
        };

        return Results.Ok(response);
    }

    private static string ResolveDisplayName(string? storedName, string email)
    {
        if (!string.IsNullOrWhiteSpace(storedName))
        {
            return storedName.Trim();
        }

        var atIndex = email.IndexOf('@');
        return atIndex > 0 ? email[..atIndex] : email;
    }

    private static WhoAmIPlanResponse BuildPlanResponse(Subscription? subscription)
    {
        if (subscription?.Plan is { } plan)
        {
            return new WhoAmIPlanResponse
            {
                Name = string.IsNullOrWhiteSpace(plan.Name) ? "Custom" : plan.Name,
                Code = string.IsNullOrWhiteSpace(plan.Code) ? "custom" : plan.Code,
                Interval = ResolveInterval(subscription.Interval),
                MonthlyLimit = plan.MaxPdfsPerMonth > 0 ? plan.MaxPdfsPerMonth : PdfJobHelper.DefaultMonthlyLimit,
                PriceCents = plan.MonthlyPriceCents
            };
        }

        return new WhoAmIPlanResponse
        {
            Name = "Free",
            Code = "free",
            Interval = "monthly",
            MonthlyLimit = PdfJobHelper.DefaultMonthlyLimit,
            PriceCents = 0
        };
    }

    private static string ResolveInterval(BillingInterval interval)
    {
        return interval switch
        {
            BillingInterval.Monthly => "monthly",
            BillingInterval.Annual => "annual",
            _ => interval.ToString().ToLowerInvariant()
        };
    }
}
