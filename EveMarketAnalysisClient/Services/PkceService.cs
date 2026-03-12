using System.Security.Cryptography;
using System.Text;

namespace EveMarketAnalysisClient.Services;

public static class PkceService
{
    private const int VerifierByteLength = 32;

    public static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(VerifierByteLength);
        return Base64UrlEncode(bytes);
    }

    public static string GenerateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    public static string GenerateState()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
