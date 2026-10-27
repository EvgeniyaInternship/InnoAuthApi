using System.Net;
using System.Net.Http.Headers;
using AuthApi.Api.Constants.Keycloak;
using AuthApi.Api.DTOs.Requests;
using AuthApi.Api.DTOs.Responses;
using AuthApi.Api.Exceptions;
using AuthApi.Api.Extensions;
using AuthApi.Api.Interfaces;
using AuthApi.Api.Options;
using Microsoft.Extensions.Options;

namespace AuthApi.Api.Services;

public sealed class AuthService(HttpClient httpClient, IOptions<KeycloakOptions> options) : IAuthService
{
    public async Task<KeycloakTokenResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var parameters = CreateTokenParameters(KeycloakGrantTypes.Password);
        parameters[KeycloakParameters.Username] = request.Email;
        parameters[KeycloakParameters.Password] = request.Password;
        parameters[KeycloakParameters.Scope] = KeycloakScopes.OpenId;

        try
        {
            return await RequestTokenAsync(parameters, cancellationToken);
        }
        catch (InvalidGrantException ex)
        {
            throw new UnauthorizedAccessException("Invalid email or password.", ex);
        }
    }

    public async Task<KeycloakTokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var parameters = CreateTokenParameters(KeycloakGrantTypes.RefreshToken);
        parameters[KeycloakParameters.RefreshToken] = request.RefreshToken;

        try
        {
            return await RequestTokenAsync(parameters, cancellationToken);
        }
        catch (InvalidGrantException ex)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token.", ex);
        }
    }

    public async Task<KeycloakTokenResponseDto> ExchangeCodeAsync(ExchangeCodeRequestDto request, CancellationToken cancellationToken = default)
    {
        var parameters = CreateTokenParameters(KeycloakGrantTypes.AuthorizationCode);
        parameters[KeycloakParameters.Code] = request.Code;
        parameters[KeycloakParameters.RedirectUri] = request.RedirectUri;

        try
        {
            return await RequestTokenAsync(parameters, cancellationToken);
        }
        catch (InvalidGrantException ex)
        {
            throw new ArgumentException("The provided authorization code is invalid or has expired.", request.Code, ex);
        }
    }

    public async Task LogoutAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>
        {
            [KeycloakParameters.ClientId] = options.Value.ClientId,
            [KeycloakParameters.ClientSecret] = options.Value.ClientSecret,
            [KeycloakParameters.RefreshToken] = request.RefreshToken
        };

        var endpoint = KeycloakEndpoints.Logout(options.Value.BaseUrl, options.Value.Realm);
        using var response = await httpClient.PostAsync(endpoint, new FormUrlEncodedContent(parameters), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var (Code, Message) = await response.ReadKeycloakErrorAsync(cancellationToken);
            throw response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
                ? new UnauthorizedAccessException($"Logout rejected by identity provider: {Message}")
                : new IdentityProviderException($"Logout failed with status {(int)response.StatusCode}: {Message}");
        }
    }

    public async Task<Guid> RegisterAsync(RegisterUserRequestDto request, CancellationToken cancellationToken = default)
    {
        var adminToken = await GetServiceAccountTokenAsync(cancellationToken);

        var payload = new
        {
            username = request.Email,
            email = request.Email,
            firstName = request.FirstName,
            lastName = request.LastName,
            enabled = true,
            credentials = new[]
            {
                new { type = KeycloakGrantTypes.Password, value = request.Password, temporary = false }
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, KeycloakEndpoints.AdminUsers(options.Value.BaseUrl, options.Value.Realm))
        {
            Content = JsonContent.Create(payload),
            Headers = { Authorization = new AuthenticationHeaderValue(KeycloakSchemes.Bearer, adminToken) }
        };

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var (Code, Message) = await response.ReadKeycloakErrorAsync(cancellationToken);

            throw response.StatusCode switch
            {
                HttpStatusCode.Conflict => new ConflictException($"User with email '{request.Email}' already exists."),
                HttpStatusCode.BadRequest => new ArgumentException($"User registration failed: {Message}"),
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new InvalidOperationException("API service account lacks 'manage-users' permissions in Keycloak."),
                _ => new IdentityProviderException($"Registration failed with status {(int)response.StatusCode}: {Message}")
            };
        }

        var location = response.Headers.Location
            ?? throw new InvalidOperationException("Keycloak response missing Location header.");

        return Guid.Parse(location.Segments.Last().TrimEnd('/'));
    }

    private Dictionary<string, string> CreateTokenParameters(string grantType) => new()
    {
        [KeycloakParameters.GrantType] = grantType,
        [KeycloakParameters.ClientId] = options.Value.ClientId,
        [KeycloakParameters.ClientSecret] = options.Value.ClientSecret
    };

    private async Task<KeycloakTokenResponseDto> RequestTokenAsync(Dictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var endpoint = KeycloakEndpoints.Token(options.Value.BaseUrl, options.Value.Realm);
        using var response = await httpClient.PostAsync(endpoint, new FormUrlEncodedContent(parameters), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var (Code, Message) = await response.ReadKeycloakErrorAsync(cancellationToken);

            if (string.Equals(Code, "invalid_grant", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidGrantException(Message);
            }

            throw new IdentityProviderException(Message);
        }

        return await response.Content.ReadFromJsonAsync<KeycloakTokenResponseDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Keycloak returned an empty token payload.");
    }

    private async Task<string> GetServiceAccountTokenAsync(CancellationToken cancellationToken)
    {
        var parameters = CreateTokenParameters(KeycloakGrantTypes.ClientCredentials);

        try
        {
            var token = await RequestTokenAsync(parameters, cancellationToken);
            return token.AccessToken;
        }
        catch (IdentityProviderException ex)
        {
            throw new InvalidOperationException("Failed to acquire service account token. Verify Keycloak client credentials.", ex);
        }
    }
}