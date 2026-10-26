using AuthApi.Api.Constants.Cookie;

namespace AuthApi.Api.Extensions;

public static class CookieExtensions
{
    public static void AppendAuthCookies(
        this HttpResponse response,
        string accessToken,
        int accessExpiresInSeconds,
        string refreshToken,
        int refreshExpiresInSeconds)
    {
        response.Cookies.Append(TokenCookie.AccessToken, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true, 
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddSeconds(accessExpiresInSeconds),
            Path = "/"
        });

        response.Cookies.Append(TokenCookie.RefreshToken, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddSeconds(refreshExpiresInSeconds),
            Path = "/Auth"
        });
    }

    public static string? GetRefreshToken(this HttpRequest request) =>
        request.Cookies[TokenCookie.RefreshToken];

    public static void DeleteAuthCookies(this HttpResponse response)
    {
        response.Cookies.Delete(TokenCookie.AccessToken, new CookieOptions { Path = "/" });
        response.Cookies.Delete(TokenCookie.RefreshToken, new CookieOptions { Path = "/Auth" });
    }
}