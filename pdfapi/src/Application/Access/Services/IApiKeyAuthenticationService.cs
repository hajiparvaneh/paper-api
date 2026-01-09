using PaperAPI.Application.Access.Responses;

namespace PaperAPI.Application.Access.Services;

public interface IApiKeyAuthenticationService
{
    Task<ApiKeyValidationResult?> AuthenticateAsync(string providedKey, CancellationToken cancellationToken);
}
