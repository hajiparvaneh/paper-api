using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
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

public static class GeneratePdfEndpoint
{
    private const int WaitTimeoutSeconds = 10;
    private const int MaxWaitWindowSeconds = 30;
    private const int RetryAfterSeconds = 3;
    private const string IdempotencyConstraintName = "IX_pdf_jobs_UserId_IdempotencyKey";
    private const string UniqueConstraintViolationSqlState = "23505";
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions PayloadHashSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/generate", HandleAsync)
            .WithName("GeneratePdf");
    }

    internal static async Task<IResult> HandleAsync(
        GeneratePdfRequest request,
        HttpContext httpContext,
        IPdfJobQueue queue,
        IPdfJobRepository jobRepository,
        ISubscriptionRepository subscriptionRepository,
        IUsageRecordRepository usageRecordRepository,
        IPdfRenderer renderer,
        IJobWaiterRegistry jobWaiterRegistry,
        IValidator<GeneratePdfRequest> validator,
        IOptions<AuthOptions> authOptions,
        ITokenService tokenService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("GeneratePdfEndpoint");
        var apiKeyId = PdfJobHelper.TryGetApiKeyId(httpContext);
        var preferOptions = ParsePreferHeader(httpContext.Request.Headers);
        var (idempotencyKey, idempotencyError) = ParseIdempotencyKey(httpContext);
        if (idempotencyError is not null)
        {
            return idempotencyError;
        }

        if (request is null)
        {
            return Results.Json(new ErrorResponse("invalid_payload", "Request body is required.", httpContext.TraceIdentifier), statusCode: StatusCodes.Status400BadRequest);
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var details = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            var status = validation.Errors.Any(e => e.ErrorMessage.Contains("500KB", StringComparison.OrdinalIgnoreCase))
                ? StatusCodes.Status413PayloadTooLarge
                : StatusCodes.Status400BadRequest;
            return Results.Json(new ErrorResponse("invalid_payload", details, httpContext.TraceIdentifier), statusCode: status);
        }

        var hasUser = PdfJobHelper.TryGetUserId(httpContext, authOptions.Value, tokenService, out var userId);

        if (!hasUser)
        {
            logger.LogInformation("No session detected; rendering synchronously with API key auth");
            return Results.Stream(
                async responseStream =>
                {
                    await renderer.RenderAsync(request.Html!, request.Options ?? new PdfOptions(), responseStream, cancellationToken);
                },
                contentType: "application/pdf",
                fileDownloadName: "document.pdf");
        }

        var planContext = await PdfJobHelper.GetPlanContextAsync(subscriptionRepository, userId, cancellationToken);
        var currentMonthUsage = await PdfJobHelper.GetMonthlyUsageAsync(usageRecordRepository, userId, cancellationToken);

        if (currentMonthUsage >= planContext.MonthlyLimit)
        {
            return Results.Json(
                new ErrorResponse("quota_exceeded", "Monthly PDF quota exceeded for the current plan.", httpContext.TraceIdentifier),
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        logger.LogInformation("Received PDF generation request (dual-mode)");

        var now = DateTimeOffset.UtcNow;
        var html = request.Html!;
        var inputSizeBytes = Encoding.UTF8.GetByteCount(html);

        string? payloadHash = null;
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            payloadHash = ComputePayloadHash(request);
            var existingJob = await jobRepository.GetByIdempotencyKeyAsync(userId, idempotencyKey, cancellationToken);
            if (existingJob is not null)
            {
                if (existingJob.IdempotencyKeyExpiresAt.HasValue && existingJob.IdempotencyKeyExpiresAt.Value < now)
                {
                    await jobRepository.ClearIdempotencyKeyAsync(existingJob.Id, cancellationToken);
                }
                else if (!string.Equals(existingJob.IdempotencyHash, payloadHash, StringComparison.Ordinal))
                {
                    return Results.Json(
                        new ErrorResponse("idempotency_conflict", "Idempotency-Key is already associated with a different payload.", httpContext.TraceIdentifier),
                        statusCode: StatusCodes.Status409Conflict);
                }
                else
                {
                    logger.LogInformation("Reusing idempotent PDF job {JobId}", existingJob.Id);
                    return BuildAcceptedJobResponse(httpContext, existingJob);
                }
            }
        }

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

        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            job.IdempotencyKey = idempotencyKey;
            job.IdempotencyHash = payloadHash!;
            job.IdempotencyKeyExpiresAt = now.Add(IdempotencyWindow);
        }

        try
        {
            await jobRepository.AddAsync(job, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsIdempotencyConstraintViolation(ex))
        {
            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                var duplicate = await jobRepository.GetByIdempotencyKeyAsync(userId, idempotencyKey, cancellationToken);
                if (duplicate is not null &&
                    duplicate.IdempotencyKeyExpiresAt.HasValue &&
                    duplicate.IdempotencyKeyExpiresAt.Value >= now &&
                    string.Equals(duplicate.IdempotencyHash, payloadHash, StringComparison.Ordinal))
                {
                    logger.LogInformation("Detected concurrent idempotent request for job {JobId}", duplicate.Id);
                    return BuildAcceptedJobResponse(httpContext, duplicate);
                }
            }

            throw;
        }

        await queue.EnqueueAsync(job, cancellationToken);

        var shouldAttemptSync = !preferOptions.ForceAsync && preferOptions.WaitSeconds > 0;
        if (shouldAttemptSync)
        {
            var waitTask = jobWaiterRegistry.WaitForCompletionAsync(job.Id, cancellationToken);
            var delayTask = Task.Delay(TimeSpan.FromSeconds(preferOptions.WaitSeconds), cancellationToken);
            var completedTask = await Task.WhenAny(waitTask, delayTask);

            if (completedTask == waitTask)
            {
                var completedJob = await waitTask;
                if (completedJob.Status == PdfJobStatus.Succeeded &&
                    !string.IsNullOrWhiteSpace(completedJob.OutputPath) &&
                    File.Exists(completedJob.OutputPath))
                {
                    logger.LogInformation("PDF job {JobId} completed synchronously", completedJob.Id);
                    return Results.Stream(
                        async responseStream =>
                        {
                            await using var pdfStream = new FileStream(
                                completedJob.OutputPath,
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

                if (completedJob.Status == PdfJobStatus.Failed)
                {
                    logger.LogWarning("PDF job {JobId} failed", completedJob.Id);
                    return Results.Json(
                        new ErrorResponse("generation_failed", completedJob.ErrorMessage ?? "PDF generation failed.", httpContext.TraceIdentifier),
                        statusCode: StatusCodes.Status500InternalServerError);
                }

                job.Status = completedJob.Status;
            }
        }

        logger.LogInformation("PDF job {JobId} continuing asynchronously", job.Id);
        return BuildAcceptedJobResponse(httpContext, job);
    }

    private static IResult BuildAcceptedJobResponse(HttpContext httpContext, PdfJob job)
    {
        var response = PdfJobResponseFactory.Create(job, includeDownloadUrl: true, includePendingDownloadUrl: true);
        SetAcceptedHeaders(httpContext, response.JobStatusUrl);
        return Results.Json(response, statusCode: StatusCodes.Status202Accepted);
    }

    private static void SetAcceptedHeaders(HttpContext context, string? jobStatusUrl)
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
        var normalizedPath = relativePath.StartsWith('/')
            ? relativePath
            : $"/{relativePath}";
        return $"{scheme}://{host}{pathBase}{normalizedPath}";
    }

    private static PreferOptions ParsePreferHeader(IHeaderDictionary headers)
    {
        var forceAsync = false;
        var waitSeconds = WaitTimeoutSeconds;

        if (headers.TryGetValue("Prefer", out var values))
        {
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                foreach (var preference in value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()))
                {
                    if (preference.Equals("respond-async", StringComparison.OrdinalIgnoreCase))
                    {
                        forceAsync = true;
                    }
                    else if (preference.StartsWith("wait=", StringComparison.OrdinalIgnoreCase))
                    {
                        var segment = preference["wait=".Length..];
                        if (int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                        {
                            waitSeconds = Math.Clamp(parsed, 0, MaxWaitWindowSeconds);
                        }
                    }
                }
            }
        }

        return new PreferOptions(forceAsync, waitSeconds);
    }

    private static (string? Key, IResult? Error) ParseIdempotencyKey(HttpContext httpContext)
    {
        if (!httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var values))
        {
            return (null, null);
        }

        var key = values.ToString().Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return (null, null);
        }

        if (key.Length > 128)
        {
            return (null, Results.Json(
                new ErrorResponse("invalid_idempotency_key", "Idempotency-Key must be 128 characters or fewer.", httpContext.TraceIdentifier),
                statusCode: StatusCodes.Status400BadRequest));
        }

        // Validate that the key contains only safe characters (alphanumeric, dash, underscore, colon)
        if (!key.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ':'))
        {
            return (null, Results.Json(
                new ErrorResponse("invalid_idempotency_key", "Idempotency-Key must contain only alphanumeric characters, dashes, underscores, or colons.", httpContext.TraceIdentifier),
                statusCode: StatusCodes.Status400BadRequest));
        }

        return (key, null);
    }

    private static string ComputePayloadHash(GeneratePdfRequest request)
    {
        var payload = new
        {
            request.Html,
            request.Options
        };

        var json = JsonSerializer.Serialize(payload, PayloadHashSerializerOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static bool IsIdempotencyConstraintViolation(DbUpdateException exception)
    {
        if (exception.InnerException is not PostgresException postgresException)
        {
            return false;
        }

        // Primary check: specific idempotency constraint name.
        if (postgresException.ConstraintName == IdempotencyConstraintName)
        {
            return true;
        }

        // Fallback: any unique-constraint violation (SQLSTATE 23505).
        return string.Equals(
            postgresException.SqlState,
            UniqueConstraintViolationSqlState,
            StringComparison.Ordinal);
    }

    private sealed record PreferOptions(bool ForceAsync, int WaitSeconds);
}
