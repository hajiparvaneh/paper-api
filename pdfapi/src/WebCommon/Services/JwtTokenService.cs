using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PaperAPI.Application.Identity.Tokens;
using PaperAPI.WebCommon.Options;

namespace PaperAPI.WebCommon.Services;

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly AuthOptions _options;
    private readonly byte[] _signingKey;
    private readonly TokenValidationParameters _validationParameters;

    public JwtTokenService(IOptions<AuthOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.JwtSecret) || _options.JwtSecret.Length < 32)
        {
            throw new InvalidOperationException("Auth:JwtSecret must be at least 32 characters long.");
        }

        _signingKey = Encoding.UTF8.GetBytes(_options.JwtSecret);
        _validationParameters = BuildValidationParameters();
    }

    public string GenerateToken(Guid userId, string email)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(_options.TokenLifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expires.UtcDateTime,
            NotBefore = now.UtcDateTime,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_signingKey), SecurityAlgorithms.HmacSha256)
        };

        var token = _tokenHandler.CreateToken(descriptor);
        return _tokenHandler.WriteToken(token);
    }

    public bool TryValidate(string token, out Guid userId)
    {
        userId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var principal = _tokenHandler.ValidateToken(token, _validationParameters, out _);
            var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ??
                          principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(subject, out userId);
        }
        catch (SecurityTokenException)
        {
            return false;
        }
    }

    private TokenValidationParameters BuildValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(_signingKey),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    }
}
