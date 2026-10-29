using AuthApi.Api.Constants.Keycloak;
using AuthApi.Api.DTOs.Requests;

namespace AuthApi.Api.Mappings;

public static class AuthMappingExtensions
{
    public static object ToKeycloakUserPayload(this RegisterUserRequestDto dto)
    {
        return new
        {
            username = dto.Email,
            email = dto.Email,
            firstName = dto.FirstName,
            lastName = dto.LastName,
            enabled = true,
            credentials = new[]
            {
                new
                {
                    type = KeycloakGrantTypes.Password,
                    value = dto.Password,
                    temporary = false
                }
            },
            attributes = new Dictionary<string, string[]>
            {
                ["role"] = [dto.Role]
            }
        };
    }

    public static string BuildAuthorizationUrl(
        string authorizeEndpoint,
        string clientId,
        string redirectUri,
        string state,
        string codeChallenge)
    {
        var query = new Dictionary<string, string>
        {
            [KeycloakParameters.ClientId] = clientId,
            [KeycloakParameters.RedirectUri] = redirectUri,
            [KeycloakParameters.ResponseType] = KeycloakResponseTypes.Code,
            [KeycloakParameters.Scope] = KeycloakScopes.OpenId,
            [KeycloakParameters.State] = state,
            [KeycloakParameters.CodeChallenge] = codeChallenge,
            [KeycloakParameters.CodeChallengeMethod] = KeycloakCodeChallengeMethods.S256
        };

        var queryString = string.Join(
            "&",
            query.Select(parameter => $"{parameter.Key}={Uri.EscapeDataString(parameter.Value)}"));

        return $"{authorizeEndpoint}?{queryString}";
    }

    public static Dictionary<string, string> ToAuthorizationCodeParameters(
        string clientId,
        string clientSecret,
        string code,
        string redirectUri,
        string codeVerifier)
    {
        return new Dictionary<string, string>
        {
            [KeycloakParameters.GrantType] = KeycloakGrantTypes.AuthorizationCode,
            [KeycloakParameters.ClientId] = clientId,
            [KeycloakParameters.ClientSecret] = clientSecret,
            [KeycloakParameters.Code] = code,
            [KeycloakParameters.RedirectUri] = redirectUri,
            [KeycloakParameters.CodeVerifier] = codeVerifier
        };
    }

    public static Dictionary<string, string> ToTokenParameters(
        this RefreshTokenRequestDto dto,
        string clientId,
        string clientSecret)
    {
        return new Dictionary<string, string>
        {
            [KeycloakParameters.GrantType] = KeycloakGrantTypes.RefreshToken,
            [KeycloakParameters.ClientId] = clientId,
            [KeycloakParameters.ClientSecret] = clientSecret,
            [KeycloakParameters.RefreshToken] = dto.RefreshToken
        };
    }

    public static Dictionary<string, string> ToClientCredentialsParameters(
        string clientId,
        string clientSecret)
    {
        return new Dictionary<string, string>
        {
            [KeycloakParameters.GrantType] = KeycloakGrantTypes.ClientCredentials,
            [KeycloakParameters.ClientId] = clientId,
            [KeycloakParameters.ClientSecret] = clientSecret
        };
    }

    public static Dictionary<string, string> ToLogoutParameters(
        this RefreshTokenRequestDto dto,
        string clientId,
        string clientSecret)
    {
        return new Dictionary<string, string>
        {
            [KeycloakParameters.ClientId] = clientId,
            [KeycloakParameters.ClientSecret] = clientSecret,
            [KeycloakParameters.RefreshToken] = dto.RefreshToken
        };
    }
}