using Microsoft.AspNetCore.Http;
using PaperAPI.WebCommon.Models.Responses;

namespace PaperAPI.PdfApi.Endpoints;

internal static class EndpointHelpers
{
    public static IResult UnauthorizedResult(HttpContext? context = null)
    {
        var requestId = context?.TraceIdentifier;
        return Results.Json(new ErrorResponse("unauthorized", "Authentication is required.", requestId), statusCode: StatusCodes.Status401Unauthorized);
    }
}
