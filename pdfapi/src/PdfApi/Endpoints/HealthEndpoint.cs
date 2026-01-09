using Microsoft.AspNetCore.Routing;

namespace PaperAPI.PdfApi.Endpoints;

public static class HealthEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .WithName("Health");
    }
}
