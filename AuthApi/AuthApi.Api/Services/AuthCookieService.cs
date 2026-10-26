using AuthApi.Api.Extensions;
using AuthApi.Api.Interfaces;
using AuthApi.Api.Options;
using Microsoft.Extensions.Options;

namespace AuthApi.Api.Services;

public sealed class AuthCookieService(IOptions<KeycloakOptions> options) : IAuthCookieService
{

    public void SetAuthCookies(HttpResponse response, string accessToken, int accessExpiresIn, string refreshToken, int refreshExpiresIn)
    {
        response.AppendAuthCookies(
            accessToken,
            accessExpiresIn,
            refreshToken,
            refreshExpiresIn
        );
    }

    public void ClearAuthCookies(HttpResponse response)
    {
        response.DeleteAuthCookies();
    }
}
