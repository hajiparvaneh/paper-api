using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PaperAPI.WebCommon.Models.Responses;
using PaperAPI.WebCommon.Options;

namespace PaperAPI.WebCommon.Middleware;

public sealed class AdminBasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AdminBasicAuthOptions _options;

    public AdminBasicAuthMiddleware(RequestDelegate next, IOptions<AdminBasicAuthOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!_options.IsConfigured)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!TryGetCredentials(context.Request.Headers.Authorization.ToString(), out var username, out var password) ||
            !FixedTimeEquals(username, _options.Username!) ||
            !FixedTimeEquals(password, _options.Password!))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Basic realm=\"Admin\"";
            context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsJsonAsync(new ErrorResponse("unauthorized", "Admin basic authentication is required.", context.TraceIdentifier));
            return;
        }

        await _next(context);
    }

    private static bool TryGetCredentials(string? authorizationHeader, out string username, out string password)
    {
        username = string.Empty;
        password = string.Empty;

        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return false;
        }

        const string prefix = "Basic ";
        if (!authorizationHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var encoded = authorizationHeader[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch (FormatException)
        {
            return false;
        }

        var separatorIndex = decoded.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return false;
        }

        username = decoded[..separatorIndex];
        password = decoded[(separatorIndex + 1)..];

        return !string.IsNullOrEmpty(username);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);

        // Always use CryptographicOperations.FixedTimeEquals to avoid timing side channels.
        // It handles different length comparisons in constant time.
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
