using PaperAPI.Application.Access.Repositories;
using PaperAPI.Application.Access.Responses;

namespace PaperAPI.Application.Access.Services;

public sealed class ApiKeyAuthenticationService : IApiKeyAuthenticationService
{
    private readonly IApiKeyRepository _apiKeyRepository;

    public ApiKeyAuthenticationService(IApiKeyRepository apiKeyRepository)
    {
        _apiKeyRepository = apiKeyRepository;
    }

    public async Task<ApiKeyValidationResult?> AuthenticateAsync(string providedKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return null;
        }

        var hash = ApiKeySecrets.Hash(providedKey);
        var apiKey = await _apiKeyRepository.GetByHashAsync(hash, cancellationToken);
        if (apiKey is null || !apiKey.IsActive)
        {
            return null;
        }

        apiKey.LastUsedAt = DateTimeOffset.UtcNow;
        await _apiKeyRepository.UpdateAsync(apiKey, cancellationToken);

        return new ApiKeyValidationResult
        {
            ApiKeyId = apiKey.Id,
            UserId = apiKey.UserId
        };
    }
}
