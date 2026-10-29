using System.Security.Cryptography;
using System.Text;

namespace AuthApi.Api.Security;

public static class Pkce
{
    public static string CreateState()
    {
        return Base64Url(RandomNumberGenerator.GetBytes(32));
    }

    public static string CreateCodeVerifier()
    {
        return Base64Url(RandomNumberGenerator.GetBytes(32));
    }

    public static string CreateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64Url(hash);
    }

    public static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
