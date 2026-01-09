using System.Security.Cryptography;
using System.Text;

namespace PaperAPI.Application.Access.Services;

internal static class ApiKeySecrets
{
    private const int PrefixLength = 12;
    private const string KeyPrefix = "pap_live_";

    public static string GenerateKey()
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        var random = Convert.ToHexString(buffer).ToLowerInvariant();
        return KeyPrefix + random;
    }

    public static string BuildPrefix(string plaintextKey)
    {
        var sanitized = plaintextKey.Replace("-", string.Empty, StringComparison.Ordinal);
        return sanitized.Length <= PrefixLength ? sanitized : sanitized[..PrefixLength];
    }

    public static string Hash(string plaintextKey)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintextKey);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
