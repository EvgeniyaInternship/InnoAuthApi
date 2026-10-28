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
        var secure = response.HttpContext.Request.IsHttps;
        response.Cookies.Append(CookieKeys.AccessToken, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddSeconds(accessExpiresInSeconds),
            Path = "/"
        });

        response.Cookies.Append(CookieKeys.RefreshToken, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddSeconds(refreshExpiresInSeconds),
            Path = "/Auth"
        });
    }

    public static void DeleteAuthCookies(this HttpResponse response)
    {
        var secure = response.HttpContext.Request.IsHttps;
        response.Cookies.Delete(CookieKeys.AccessToken, new CookieOptions
        {
            Path = "/",
            Secure = secure,
            SameSite = SameSiteMode.Strict
        });
        response.Cookies.Delete(CookieKeys.RefreshToken, new CookieOptions
        {
            Path = "/Auth",
            Secure = secure,
            SameSite = SameSiteMode.Strict
        });
    }

    public static void AppendAuthorizationFlowCookies(this HttpResponse response, string state, string codeVerifier)
    {
        var options = CreateAuthorizationFlowCookieOptions(response.HttpContext.Request.IsHttps);
        response.Cookies.Append(CookieKeys.AuthState, state, options);
        response.Cookies.Append(CookieKeys.CodeVerifier, codeVerifier, options);
    }

    public static void DeleteAuthorizationFlowCookies(this HttpResponse response)
    {
        var options = CreateAuthorizationFlowCookieOptions(response.HttpContext.Request.IsHttps);
        response.Cookies.Delete(CookieKeys.AuthState, options);
        response.Cookies.Delete(CookieKeys.CodeVerifier, options);
    }

    private static CookieOptions CreateAuthorizationFlowCookieOptions(bool isHttps)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10),
            Path = "/Auth"
        };
    }
}