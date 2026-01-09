using Microsoft.Extensions.DependencyInjection;
using PaperAPI.Application.Access.Services;
using PaperAPI.Application.Identity.Services;

namespace PaperAPI.Application.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAccessService, AccessService>();
        services.AddScoped<IApiKeyAuthenticationService, ApiKeyAuthenticationService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        return services;
    }
}
