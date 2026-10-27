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
        response.Cookies.Append(CookieKeys.AccessToken, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true, 
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddSeconds(accessExpiresInSeconds),
            Path = "/"
        });

        response.Cookies.Append(CookieKeys.RefreshToken, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddSeconds(refreshExpiresInSeconds),
            Path = "/Auth"
        });
    }

    public static void DeleteAuthCookies(this HttpResponse response)
    {
        response.Cookies.Delete(CookieKeys.AccessToken, new CookieOptions { Path = "/" });
        response.Cookies.Delete(CookieKeys.RefreshToken, new CookieOptions { Path = "/Auth" });
    }
}