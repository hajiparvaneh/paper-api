using System.Linq;
using Microsoft.AspNetCore.Http;

namespace PaperAPI.WebCommon.Middleware;

public sealed class RequestIdMiddleware
{
    private const int MaxRequestIdLength = 64;
    private readonly RequestDelegate _next;

    public RequestIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var requestId = ResolveRequestId(context.Request.Headers);
        context.TraceIdentifier = requestId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Request-Id"] = requestId;
            return Task.CompletedTask;
        });

        return _next(context);
    }

    private static string ResolveRequestId(IHeaderDictionary headers)
    {
        if (headers.TryGetValue("X-Request-Id", out var values))
        {
            var candidate = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length <= MaxRequestIdLength)
            {
                return candidate;
            }
        }

        return Guid.NewGuid().ToString("n");
    }
}
