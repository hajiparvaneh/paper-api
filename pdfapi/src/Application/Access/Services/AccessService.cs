using System.Net;
using PaperAPI.Application.Access.Exceptions;
using PaperAPI.Application.Access.Repositories;
using PaperAPI.Application.Access.Requests;
using PaperAPI.Application.Access.Responses;
using PaperAPI.Application.Identity.Repositories;
using PaperAPI.Domain.Access;

namespace PaperAPI.Application.Access.Services;

public sealed class AccessService : IAccessService
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IUserRepository _userRepository;

    public AccessService(
        IApiKeyRepository apiKeyRepository,
        IUserRepository userRepository)
    {
        _apiKeyRepository = apiKeyRepository;
        _userRepository = userRepository;
    }

    public async Task<CreateApiKeyResult> CreateApiKeyAsync(Guid userId, CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
                   ?? throw new AccessException("user_not_found", "User not found.", HttpStatusCode.NotFound);

        if (user.IsDeleted)
        {
            throw new AccessException("account_disabled", "This account is no longer active.", HttpStatusCode.Forbidden);
        }

        var name = (request?.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new AccessException("invalid_name", "API key name is required.", HttpStatusCode.BadRequest);
        }

        if (name.Length > 128)
        {
            throw new AccessException("invalid_name", "API key name must be 128 characters or fewer.", HttpStatusCode.BadRequest);
        }

        if (user.ApiKeys.Any(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AccessException("name_in_use", "You already have an API key with this name.", HttpStatusCode.Conflict);
        }

        var plaintextKey = ApiKeySecrets.GenerateKey();
        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            KeyHash = ApiKeySecrets.Hash(plaintextKey),
            Prefix = ApiKeySecrets.BuildPrefix(plaintextKey),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _apiKeyRepository.AddAsync(apiKey, cancellationToken);

        var dto = Map(apiKey);
        return new CreateApiKeyResult
        {
            Key = dto,
            PlaintextKey = plaintextKey
        };
    }

    public async Task DeactivateApiKeyAsync(Guid userId, Guid apiKeyId, CancellationToken cancellationToken)
    {
        var apiKey = await _apiKeyRepository.GetByIdAsync(apiKeyId, cancellationToken)
                     ?? throw new AccessException("key_not_found", "API key not found.", HttpStatusCode.NotFound);

        if (apiKey.UserId != userId)
        {
            throw new AccessException("forbidden", "You cannot modify this API key.", HttpStatusCode.Forbidden);
        }

        if (!apiKey.IsActive)
        {
            return;
        }

        apiKey.IsActive = false;
        await _apiKeyRepository.UpdateAsync(apiKey, cancellationToken);
    }

    public async Task ActivateApiKeyAsync(Guid userId, Guid apiKeyId, CancellationToken cancellationToken)
    {
        var apiKey = await _apiKeyRepository.GetByIdAsync(apiKeyId, cancellationToken)
                     ?? throw new AccessException("key_not_found", "API key not found.", HttpStatusCode.NotFound);

        if (apiKey.UserId != userId)
        {
            throw new AccessException("forbidden", "You cannot modify this API key.", HttpStatusCode.Forbidden);
        }

        if (apiKey.IsActive)
        {
            return;
        }

        apiKey.IsActive = true;
        await _apiKeyRepository.UpdateAsync(apiKey, cancellationToken);
    }

    public async Task RemoveApiKeyAsync(Guid userId, Guid apiKeyId, CancellationToken cancellationToken)
    {
        var apiKey = await _apiKeyRepository.GetByIdAsync(apiKeyId, cancellationToken)
                     ?? throw new AccessException("key_not_found", "API key not found.", HttpStatusCode.NotFound);

        if (apiKey.UserId != userId)
        {
            throw new AccessException("forbidden", "You cannot modify this API key.", HttpStatusCode.Forbidden);
        }

        if (apiKey.IsActive)
        {
            throw new AccessException("key_active", "Revoke the API key before removing it.", HttpStatusCode.BadRequest);
        }

        await _apiKeyRepository.DeleteAsync(apiKey, cancellationToken);
    }

    public async Task<IReadOnlyList<ApiKeyDto>> GetApiKeysAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
                   ?? throw new AccessException("user_not_found", "User not found.", HttpStatusCode.NotFound);

        if (user.IsDeleted)
        {
            throw new AccessException("account_disabled", "This account is no longer active.", HttpStatusCode.Forbidden);
        }

        var keys = await _apiKeyRepository.GetByUserAsync(userId, cancellationToken);
        return keys.Select(Map).ToArray();
    }

    private static ApiKeyDto Map(ApiKey apiKey)
    {
        return new ApiKeyDto
        {
            Id = apiKey.Id,
            Name = apiKey.Name,
            Prefix = apiKey.Prefix,
            IsActive = apiKey.IsActive,
            CreatedAt = apiKey.CreatedAt,
            LastUsedAt = apiKey.LastUsedAt
        };
    }

}
