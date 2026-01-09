using System.Net.Mime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaperAPI.Application.Access.Services;
using PaperAPI.Application.Identity.Tokens;
using PaperAPI.WebCommon.Models.Responses;
using PaperAPI.WebCommon.Options;

namespace PaperAPI.WebCommon.Middleware;

public sealed class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AppOptions _options;
    private readonly AuthOptions _authOptions;
    private readonly ITokenService _tokenService;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;

    public ApiKeyAuthMiddleware(
        RequestDelegate next,
        IOptions<AppOptions> options,
        IOptions<AuthOptions> authOptions,
        ITokenService tokenService,
        ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _authOptions = authOptions.Value;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsOptions(context.Request.Method) ||
            IsHealthEndpoint(context.Request) ||
            IsMetricsEndpoint(context.Request) ||
            !RequiresApiKey(context.Request))
        {
            await _next(context);
            return;
        }

        if (!await IsAuthorizedAsync(context) && !TryAuthenticateSession(context))
        {
            _logger.LogWarning("Unauthorized request to {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsJsonAsync(new ErrorResponse("unauthorized", "A valid API key or session token is required.", context.TraceIdentifier));
            return;
        }

        await _next(context);
    }

    private async Task<bool> IsAuthorizedAsync(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (IsConfiguredApiKey(header))
        {
            return true;
        }

        if (!TryExtractBearer(header, out var providedKey))
        {
            return false;
        }

        var authService = context.RequestServices.GetRequiredService<IApiKeyAuthenticationService>();
        var result = await authService.AuthenticateAsync(providedKey, context.RequestAborted);
        if (result is null)
        {
            return false;
        }

        context.Items["UserId"] = result.UserId;
        context.Items["ApiKeyId"] = result.ApiKeyId;
        return true;
    }

    private bool IsConfiguredApiKey(string? header)
    {
        if (!TryExtractBearer(header, out var providedKey))
        {
            return false;
        }

        return _options.ApiKeys.Any(k => string.Equals(k, providedKey, StringComparison.Ordinal));
    }

    private bool TryAuthenticateSession(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(_authOptions.CookieName, out var token))
        {
            return false;
        }

        if (_tokenService.TryValidate(token, out var userId))
        {
            context.Items["UserId"] = userId;
            return true;
        }

        return false;
    }

    private static bool TryExtractBearer(string? header, out string key)
    {
        key = string.Empty;
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        const string bearerPrefix = "Bearer ";
        if (!header.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var providedKey = header[bearerPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return false;
        }

        key = providedKey;
        return true;
    }

    private static bool IsHealthEndpoint(HttpRequest request) => string.Equals(request.Path, "/health", StringComparison.OrdinalIgnoreCase);

    private static bool IsMetricsEndpoint(HttpRequest request) => string.Equals(request.Path, "/metrics", StringComparison.OrdinalIgnoreCase);

    private static bool RequiresApiKey(HttpRequest request)
    {
        var path = request.Path.ToString();
        return path.StartsWith("/v1", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/jobs", StringComparison.OrdinalIgnoreCase);
    }
}
