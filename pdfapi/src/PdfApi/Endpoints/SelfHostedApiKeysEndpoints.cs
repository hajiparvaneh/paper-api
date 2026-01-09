using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PaperAPI.Application.Access.Exceptions;
using PaperAPI.Application.Access.Requests;
using PaperAPI.Application.Access.Responses;
using PaperAPI.Application.Access.Services;
using PaperAPI.Application.Identity.Tokens;
using PaperAPI.PdfApi.Models.Responses;
using PaperAPI.PdfApi.SelfHosted;
using PaperAPI.WebCommon.Auth;
using PaperAPI.WebCommon.Models.Responses;
using PaperAPI.WebCommon.Options;

namespace PaperAPI.PdfApi.Endpoints;

public static class SelfHostedApiKeysEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/self-hosted/api-keys");
        group.MapGet(string.Empty, GetAsync);
        group.MapPost(string.Empty, CreateAsync);
        group.MapPost("/{id:guid}/revoke", RevokeAsync);
        group.MapPost("/{id:guid}/restore", RestoreAsync);
    }

    private static async Task<IResult> GetAsync(
        HttpContext httpContext,
        IOptions<AuthOptions> authOptions,
        ITokenService tokenService,
        SelfHostedAdminService adminService,
        IAccessService accessService,
        CancellationToken cancellationToken)
    {
        var admin = await GetAdminAsync(httpContext, authOptions.Value, tokenService, adminService, cancellationToken);
        if (admin is null)
        {
            return Results.Json(new ErrorResponse("unauthorized", "Admin authentication is required."), statusCode: StatusCodes.Status401Unauthorized);
        }

        var keys = await accessService.GetApiKeysAsync(admin.Id, cancellationToken);
        var response = keys.Select(Map).ToArray();
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateAsync(
        CreateApiKeyRequest request,
        HttpContext httpContext,
        IOptions<AuthOptions> authOptions,
        ITokenService tokenService,
        SelfHostedAdminService adminService,
        IAccessService accessService,
        CancellationToken cancellationToken)
    {
        try
        {
            var admin = await GetAdminAsync(httpContext, authOptions.Value, tokenService, adminService, cancellationToken);
            if (admin is null)
            {
                return Results.Json(new ErrorResponse("unauthorized", "Admin authentication is required."), statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await accessService.CreateApiKeyAsync(admin.Id, request, cancellationToken);
            return Results.Ok(new SelfHostedCreateApiKeyResponse
            {
                Key = Map(result.Key),
                PlaintextKey = result.PlaintextKey
            });
        }
        catch (AccessException ex)
        {
            return Results.Json(new ErrorResponse(ex.Error, ex.Message), statusCode: (int)ex.StatusCode);
        }
        catch (Exception ex)
        {
            return Results.Json(new ErrorResponse("api_key_error", ex.Message), statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> RevokeAsync(
        Guid id,
        HttpContext httpContext,
        IOptions<AuthOptions> authOptions,
        ITokenService tokenService,
        SelfHostedAdminService adminService,
        IAccessService accessService,
        CancellationToken cancellationToken)
    {
        try
        {
            var admin = await GetAdminAsync(httpContext, authOptions.Value, tokenService, adminService, cancellationToken);
            if (admin is null)
            {
                return Results.Json(new ErrorResponse("unauthorized", "Admin authentication is required."), statusCode: StatusCodes.Status401Unauthorized);
            }

            await accessService.DeactivateApiKeyAsync(admin.Id, id, cancellationToken);
            return Results.NoContent();
        }
        catch (AccessException ex)
        {
            return Results.Json(new ErrorResponse(ex.Error, ex.Message), statusCode: (int)ex.StatusCode);
        }
        catch (Exception ex)
        {
            return Results.Json(new ErrorResponse("api_key_error", ex.Message), statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> RestoreAsync(
        Guid id,
        HttpContext httpContext,
        IOptions<AuthOptions> authOptions,
        ITokenService tokenService,
        SelfHostedAdminService adminService,
        IAccessService accessService,
        CancellationToken cancellationToken)
    {
        try
        {
            var admin = await GetAdminAsync(httpContext, authOptions.Value, tokenService, adminService, cancellationToken);
            if (admin is null)
            {
                return Results.Json(new ErrorResponse("unauthorized", "Admin authentication is required."), statusCode: StatusCodes.Status401Unauthorized);
            }

            await accessService.ActivateApiKeyAsync(admin.Id, id, cancellationToken);
            return Results.NoContent();
        }
        catch (AccessException ex)
        {
            return Results.Json(new ErrorResponse(ex.Error, ex.Message), statusCode: (int)ex.StatusCode);
        }
        catch (Exception ex)
        {
            return Results.Json(new ErrorResponse("api_key_error", ex.Message), statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static SelfHostedApiKeyResponse Map(ApiKeyDto dto)
    {
        return new SelfHostedApiKeyResponse
        {
            Id = dto.Id,
            Name = dto.Name,
            Prefix = dto.Prefix,
            IsActive = dto.IsActive,
            CreatedAt = dto.CreatedAt,
            LastUsedAt = dto.LastUsedAt
        };
    }

    private static async Task<PaperAPI.Domain.Identity.User?> GetAdminAsync(
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
}
