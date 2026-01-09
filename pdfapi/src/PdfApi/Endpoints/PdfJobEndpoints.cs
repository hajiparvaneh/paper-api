using System.Globalization;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PaperAPI.Application.Access.Repositories;
using PaperAPI.Application.Billing.Repositories;
using PaperAPI.Application.Identity.Tokens;
using PaperAPI.Application.Pdf;
using PaperAPI.Application.Pdf.Repositories;
using PaperAPI.Domain.Pdf;
using PaperAPI.PdfApi.Models.Requests;
using PaperAPI.PdfApi.Models.Responses;
using PaperAPI.WebCommon.Models.Responses;
using PaperAPI.WebCommon.Options;

namespace PaperAPI.PdfApi.Endpoints;

public static class PdfJobEndpoints
{
    private const int RetryAfterSeconds = 3;

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/generate-async", EnqueueAsync)
            .WithName("GeneratePdfAsync");

        app.MapGet("/jobs/{id:guid}", GetStatusAsync)
            .WithName("GetPdfJobStatus");

        app.MapGet("/jobs/{id:guid}/result", GetResultAsync)
            .WithName("GetPdfJobResult");
    }

    private static async Task<IResult> EnqueueAsync(
        GeneratePdfRequest request,
        HttpContext httpContext,
        IPdfJobQueue queue,
        IPdfJobRepository jobRepository,
        ISubscriptionRepository subscriptionRepository,
        IUsageRecordRepository usageRecordRepository,
        IValidator<GeneratePdfRequest> validator,
        IOptions<AuthOptions> authOptions,
        ITokenService tokenService,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var details = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            var status = validation.Errors.Any(e => e.ErrorMessage.Contains("500KB", StringComparison.OrdinalIgnoreCase))
                ? StatusCodes.Status413PayloadTooLarge
                : StatusCodes.Status400BadRequest;
            return Results.Json(new ErrorResponse("invalid_payload", details, httpContext.TraceIdentifier), statusCode: status);
        }

        if (!PdfJobHelper.TryGetUserId(httpContext, authOptions.Value, tokenService, out var userId))
        {
            return EndpointHelpers.UnauthorizedResult(httpContext);
        }

        var apiKeyId = PdfJobHelper.TryGetApiKeyId(httpContext);
        var planContext = await PdfJobHelper.GetPlanContextAsync(subscriptionRepository, userId, cancellationToken);
        var currentMonthUsage = await PdfJobHelper.GetMonthlyUsageAsync(usageRecordRepository, userId, cancellationToken);

        if (currentMonthUsage >= planContext.MonthlyLimit)
        {
            return Results.Json(
                new ErrorResponse("quota_exceeded", "Monthly PDF quota exceeded for the current plan.", httpContext.TraceIdentifier),
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var now = DateTimeOffset.UtcNow;
        var html = request.Html!;
        var inputSizeBytes = Encoding.UTF8.GetByteCount(html);

        var job = new PdfJob
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApiKeyId = apiKeyId,
            Html = html,
            PageSize = request.Options?.PageSize,
            Orientation = request.Options?.Orientation,
            MarginTop = request.Options?.MarginTop,
            MarginRight = request.Options?.MarginRight,
            MarginBottom = request.Options?.MarginBottom,
            MarginLeft = request.Options?.MarginLeft,
            PrintMediaType = request.Options?.PrintMediaType,
            DisableSmartShrinking = request.Options?.DisableSmartShrinking,
            EnableJavascript = request.Options?.EnableJavascript,
            DisableJavascript = request.Options?.DisableJavascript,
            HeaderLeft = request.Options?.HeaderLeft,
            HeaderCenter = request.Options?.HeaderCenter,
            HeaderRight = request.Options?.HeaderRight,
            FooterLeft = request.Options?.FooterLeft,
            FooterCenter = request.Options?.FooterCenter,
            FooterRight = request.Options?.FooterRight,
            HeaderSpacing = request.Options?.HeaderSpacing,
            FooterSpacing = request.Options?.FooterSpacing,
            HeaderHtml = request.Options?.HeaderHtml,
            FooterHtml = request.Options?.FooterHtml,
            Dpi = request.Options?.Dpi,
            Zoom = request.Options?.Zoom,
            ImageDpi = request.Options?.ImageDpi,
            ImageQuality = request.Options?.ImageQuality,
            LowQuality = request.Options?.LowQuality,
            Images = request.Options?.Images,
            NoImages = request.Options?.NoImages,
            Status = PdfJobStatus.Queued,
            PriorityWeight = planContext.PriorityWeight,
            CreatedAt = now,
            InputSizeBytes = inputSizeBytes,
            RetentionDays = planContext.LogRetentionDays,
            ExpiresAt = now.AddDays(planContext.LogRetentionDays)
        };

        await jobRepository.AddAsync(job, cancellationToken);
        await queue.EnqueueAsync(job, cancellationToken);

        var response = PdfJobResponseFactory.Create(job, includeDownloadUrl: false);
        ApplyAcceptedHeaders(httpContext, response.JobStatusUrl);

        return Results.Json(response, statusCode: StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> GetStatusAsync(
        Guid id,
        HttpContext httpContext,
        IPdfJobRepository jobRepository,
        IOptions<AuthOptions> authOptions,
        ITokenService tokenService,
        CancellationToken cancellationToken)
    {
        if (!PdfJobHelper.TryGetUserId(httpContext, authOptions.Value, tokenService, out var userId))
        {
            return EndpointHelpers.UnauthorizedResult(httpContext);
        }

        var job = await jobRepository.GetByIdForUserAsync(id, userId, cancellationToken);
        if (job is null)
        {
            return Results.NotFound();
        }

        var response = PdfJobResponseFactory.Create(job, includeDownloadUrl: true);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetResultAsync(
        Guid id,
        HttpContext httpContext,
        IPdfJobRepository jobRepository,
        IOptions<AuthOptions> authOptions,
        ITokenService tokenService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("PdfJobEndpoints");

        if (!PdfJobHelper.TryGetUserId(httpContext, authOptions.Value, tokenService, out var userId))
        {
            return EndpointHelpers.UnauthorizedResult(httpContext);
        }

        var job = await jobRepository.GetByIdForUserAsync(id, userId, cancellationToken);
        if (job is null)
        {
            return Results.NotFound();
        }

        if (job.Status != PdfJobStatus.Succeeded)
        {
            return Results.NotFound();
        }

        if (string.IsNullOrWhiteSpace(job.OutputPath) || !File.Exists(job.OutputPath))
        {
            logger.LogWarning("PdfJob {JobId} marked succeeded but output missing at {Path}", job.Id, job.OutputPath);
            return Results.Json(
                new ErrorResponse("file_missing", "PDF output is unavailable. Please retry.", httpContext.TraceIdentifier),
                statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Stream(
            async responseStream =>
            {
                await using var pdfStream = new FileStream(
                    job.OutputPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    options: FileOptions.Asynchronous | FileOptions.SequentialScan);
                await pdfStream.CopyToAsync(responseStream, 81920, cancellationToken);
            },
            contentType: "application/pdf",
            fileDownloadName: "document.pdf");
    }

    private static void ApplyAcceptedHeaders(HttpContext context, string? jobStatusUrl)
    {
        if (!string.IsNullOrWhiteSpace(jobStatusUrl))
        {
            context.Response.Headers["Location"] = BuildAbsoluteUrl(context, jobStatusUrl!);
        }

        context.Response.Headers["Retry-After"] = RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
    }

    private static string BuildAbsoluteUrl(HttpContext context, string relativePath)
    {
        var scheme = string.IsNullOrWhiteSpace(context.Request.Scheme) ? "https" : context.Request.Scheme;
        var host = context.Request.Host.HasValue ? context.Request.Host.Value : context.Request.Headers.Host.ToString();
        if (string.IsNullOrWhiteSpace(host))
        {
            host = "localhost";
        }

        var pathBase = context.Request.PathBase.HasValue ? context.Request.PathBase.Value : string.Empty;
        var normalizedPath = relativePath.StartsWith('/') ? relativePath : $"/{relativePath}";
        return $"{scheme}://{host}{pathBase}{normalizedPath}";
    }
}
