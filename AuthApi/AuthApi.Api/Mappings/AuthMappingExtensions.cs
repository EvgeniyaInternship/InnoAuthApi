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
            }
        };
    }

    public static Dictionary<string, string> ToTokenParameters(
        this ExchangeCodeRequestDto dto,
        string clientId,
        string clientSecret)
    {
        return new Dictionary<string, string>
        {
            [KeycloakParameters.GrantType] = KeycloakGrantTypes.AuthorizationCode,
            [KeycloakParameters.ClientId] = clientId,
            [KeycloakParameters.ClientSecret] = clientSecret,
            [KeycloakParameters.Code] = dto.Code,
            [KeycloakParameters.RedirectUri] = dto.RedirectUri
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

    public static Dictionary<string, string> ToTokenParameters(
        this LoginRequestDto dto,
        string clientId,
        string clientSecret)
    {
        return new Dictionary<string, string>
        {
            [KeycloakParameters.GrantType] = KeycloakGrantTypes.Password,
            [KeycloakParameters.ClientId] = clientId,
            [KeycloakParameters.ClientSecret] = clientSecret,
            [KeycloakParameters.Username] = dto.Email,
            [KeycloakParameters.Password] = dto.Password,
            [KeycloakParameters.Scope] = KeycloakScopes.OpenId
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