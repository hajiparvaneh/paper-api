namespace PaperAPI.Application.Identity.Tokens;

public interface ITokenService
{
    string GenerateToken(Guid userId, string email);

    bool TryValidate(string token, out Guid userId);
}
