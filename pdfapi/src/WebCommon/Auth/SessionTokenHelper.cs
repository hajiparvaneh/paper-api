using Microsoft.AspNetCore.Http;
using PaperAPI.Application.Identity.Tokens;
using PaperAPI.WebCommon.Options;

namespace PaperAPI.WebCommon.Auth;

public static class SessionTokenHelper
{
    public static bool TryGetUserId(HttpContext httpContext, AuthOptions options, ITokenService tokenService, out Guid userId)
    {
        userId = Guid.Empty;
        if (!httpContext.Request.Cookies.TryGetValue(options.CookieName, out var token))
        {
            return false;
        }

        return tokenService.TryValidate(token, out userId);
    }
}
