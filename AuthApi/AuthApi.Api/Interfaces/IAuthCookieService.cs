namespace AuthApi.Api.Interfaces;

public interface IAuthCookieService
{
    void SetAuthCookies(HttpResponse response, string accessToken, int accessExpiresIn, string refreshToken, int refreshExpiresIn);
    void ClearAuthCookies(HttpResponse response);
}
