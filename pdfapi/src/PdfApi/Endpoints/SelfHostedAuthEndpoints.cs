using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PaperAPI.Application.Identity.Tokens;
using PaperAPI.PdfApi.Models.Requests;
using PaperAPI.PdfApi.Models.Responses;
using PaperAPI.PdfApi.SelfHosted;
using PaperAPI.WebCommon.Auth;
using PaperAPI.WebCommon.Models.Responses;
using PaperAPI.WebCommon.Options;

namespace PaperAPI.PdfApi.Endpoints;

public static class SelfHostedAuthEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/self-hosted");
        group.MapGet("/status", GetStatusAsync);
        group.MapGet("/me", GetMeAsync);
        group.MapPost("/setup", SetupAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/logout", LogoutAsync);
    }

    private static async Task<IResult> GetStatusAsync(
        SelfHostedAdminService adminService,
        CancellationToken cancellationToken)
    {
        var admin = await adminService.GetAdminAsync(cancellationToken);
        return Results.Ok(new SelfHostedStatusResponse
        {
            IsConfigured = admin is not null,
            Username = admin?.Email
        });
    }

    private static async Task<IResult> GetMeAsync(
        HttpContext httpContext,
        IOptions<AuthOptions> authOptions,
        ITokenService tokenService,
        SelfHostedAdminService adminService,
        CancellationToken cancellationToken)
    {
        var admin = await GetAdminFromSessionAsync(httpContext, authOptions.Value, tokenService, adminService, cancellationToken);
        if (admin is null)
        {
            return Results.Json(new ErrorResponse("unauthorized", "Admin authentication is required."), statusCode: StatusCodes.Status401Unauthorized);
        }

        return Results.Ok(new SelfHostedMeResponse { Username = admin.Email });
    }

    private static async Task<IResult> SetupAsync(
        SelfHostedSetupRequest request,
        HttpContext httpContext,
        IOptions<AuthOptions> authOptions,
        ITokenService tokenService,
        SelfHostedAdminService adminService,
        CancellationToken cancellationToken)
    {
        try
        {
            var admin = await adminService.CreateAdminAsync(request.Username, request.Password, cancellationToken);
            IssueSessionCookie(httpContext, authOptions.Value, tokenService, admin);
            return Results.Ok(new SelfHostedMeResponse { Username = admin.Email });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Json(new ErrorResponse("admin_exists", ex.Message), statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException ex)
        {
            return Results.Json(new ErrorResponse("invalid_request", ex.Message), statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> LoginAsync(
        SelfHostedLoginRequest request,
        HttpContext httpContext,
        IOptions<AuthOptions> authOptions,
        ITokenService tokenService,
        SelfHostedAdminService adminService,
        CancellationToken cancellationToken)
    {
        var admin = await adminService.AuthenticateAsync(request.Username, request.Password, cancellationToken);
        if (admin is null)
        {
            return Results.Json(new ErrorResponse("invalid_credentials", "Invalid username or password."), statusCode: StatusCodes.Status401Unauthorized);
        }

        IssueSessionCookie(httpContext, authOptions.Value, tokenService, admin);
        return Results.Ok(new SelfHostedMeResponse { Username = admin.Email });
    }

    private static IResult LogoutAsync(
        HttpContext httpContext,
        IOptions<AuthOptions> authOptions)
    {
        var options = CreateCookieOptions(httpContext, authOptions.Value, expireImmediately: true);
        httpContext.Response.Cookies.Delete(authOptions.Value.CookieName, options);
        return Results.NoContent();
    }

    private static async Task<PaperAPI.Domain.Identity.User?> GetAdminFromSessionAsync(
        HttpContext httpContext,
        AuthOptions authOptions,
        ITokenService tokenService,
        SelfHostedAdminService adminService,
        CancellationToken cancellationToken)
    {
        if (!SessionTokenHelper.TryGetUserId(httpContext, authOptions, tokenService, out var userId))
        {
            return null;
        }

        var admin = await adminService.GetAdminAsync(cancellationToken);
        if (admin is null || admin.Id != userId)
        {
            return null;
        }

        return admin;
    }

    private static void IssueSessionCookie(
        HttpContext httpContext,
        AuthOptions options,
        ITokenService tokenService,
        PaperAPI.Domain.Identity.User admin)
    {
        var token = tokenService.GenerateToken(admin.Id, admin.Email);
        var cookieOptions = CreateCookieOptions(httpContext, options);
        httpContext.Response.Cookies.Append(options.CookieName, token, cookieOptions);
    }

    private static CookieOptions CreateCookieOptions(HttpContext httpContext, AuthOptions options, bool expireImmediately = false)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = options.CookieHttpOnly,
            Secure = options.CookieSecure,
            SameSite = options.CookieSameSite,
            Path = string.IsNullOrWhiteSpace(options.CookiePath) ? "/" : options.CookiePath
        };

        var resolvedDomain = ResolveCookieDomain(httpContext, options);
        if (!string.IsNullOrWhiteSpace(resolvedDomain))
        {
            cookieOptions.Domain = resolvedDomain;
        }

        cookieOptions.Expires = expireImmediately
            ? DateTimeOffset.UtcNow.AddDays(-1)
            : DateTimeOffset.UtcNow.AddMinutes(options.TokenLifetimeMinutes);

        return cookieOptions;
    }

    private static string? ResolveCookieDomain(HttpContext httpContext, AuthOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.CookieDomain))
        {
            return options.CookieDomain;
        }

        if (httpContext.Request.Headers.TryGetValue("Origin", out var originValues))
        {
            foreach (var origin in originValues)
            {
                if (Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
                {
                    var host = originUri.Host;
                    if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    if (IPAddress.TryParse(host, out _))
                    {
                        return null;
                    }

                    return host;
                }
            }
        }

        return null;
    }
}
