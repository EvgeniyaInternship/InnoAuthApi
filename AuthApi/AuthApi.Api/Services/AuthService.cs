using System.Net;
using System.Net.Http.Headers;
using AuthApi.Api.Configuration;
using AuthApi.Api.Constants.Keycloak;
using AuthApi.Api.DTOs.Requests;
using AuthApi.Api.DTOs.Responses;
using AuthApi.Api.Exceptions;
using AuthApi.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace AuthApi.Api.Services;

public sealed class AuthService(HttpClient httpClient, IOptions<KeycloakOptions> options) : IAuthService
{
    public async Task<KeycloakTokenResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var parameters = CreateTokenParameters(GrantTypes.Password);
        parameters[Parameters.Username] = request.Email;
        parameters[Parameters.Password] = request.Password;
        parameters[Parameters.Scope] = Scopes.OpenId;

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
        var parameters = CreateTokenParameters(GrantTypes.RefreshToken);
        parameters[Parameters.RefreshToken] = request.RefreshToken;

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
        var parameters = CreateTokenParameters(GrantTypes.AuthorizationCode);
        parameters[Parameters.Code] = request.Code;
        parameters[Parameters.RedirectUri] = request.RedirectUri;

        try
        {
            return await RequestTokenAsync(parameters, cancellationToken);
        }
        catch (InvalidGrantException ex)
        {
            throw new ArgumentException("The provided authorization code is invalid or has expired.", nameof(request.Code), ex);
        }
    }

    public async Task LogoutAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>
        {
            [Parameters.ClientId] = options.Value.ClientId,
            [Parameters.ClientSecret] = options.Value.ClientSecret,
            [Parameters.RefreshToken] = request.RefreshToken
        };

        var endpoint = Endpoints.Logout(options.Value.BaseUrl, options.Value.Realm);
        using var response = await httpClient.PostAsync(endpoint, new FormUrlEncodedContent(parameters), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, cancellationToken);
            throw response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
                ? new UnauthorizedAccessException($"Logout rejected by identity provider: {error.Message}")
                : new IdentityProviderException($"Logout failed with status {(int)response.StatusCode}: {error.Message}");
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
                new { type = CredentialTypes.Password, value = request.Password, temporary = false }
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, Endpoints.AdminUsers(options.Value.BaseUrl, options.Value.Realm))
        {
            Content = JsonContent.Create(payload),
            Headers = { Authorization = new AuthenticationHeaderValue(Schemes.Bearer, adminToken) }
        };

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, cancellationToken);

            throw response.StatusCode switch
            {
                HttpStatusCode.Conflict => new ConflictException($"User with email '{request.Email}' already exists."),
                HttpStatusCode.BadRequest => new ArgumentException($"User registration failed: {error.Message}"),
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new InvalidOperationException("API service account lacks 'manage-users' permissions in Keycloak."),
                _ => new IdentityProviderException($"Registration failed with status {(int)response.StatusCode}: {error.Message}")
            };
        }

        var location = response.Headers.Location
            ?? throw new InvalidOperationException("Keycloak response missing Location header.");

        return Guid.Parse(location.Segments.Last().TrimEnd('/'));
    }

    private Dictionary<string, string> CreateTokenParameters(string grantType) => new()
    {
        [Parameters.GrantType] = grantType,
        [Parameters.ClientId] = options.Value.ClientId,
        [Parameters.ClientSecret] = options.Value.ClientSecret
    };

    private async Task<KeycloakTokenResponseDto> RequestTokenAsync(Dictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var endpoint = Endpoints.Token(options.Value.BaseUrl, options.Value.Realm);
        using var response = await httpClient.PostAsync(endpoint, new FormUrlEncodedContent(parameters), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, cancellationToken);

            if (string.Equals(error.Code, "invalid_grant", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidGrantException(error.Message);
            }

            throw new IdentityProviderException(error.Message);
        }

        return await response.Content.ReadFromJsonAsync<KeycloakTokenResponseDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Keycloak returned an empty token payload.");
    }

    private async Task<string> GetServiceAccountTokenAsync(CancellationToken cancellationToken)
    {
        var parameters = CreateTokenParameters(GrantTypes.ClientCredentials);

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

    private static async Task<(string? Code, string Message)> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<KeycloakErrorDto>(cancellationToken: cancellationToken);
            var message = payload?.ErrorDescription ?? payload?.ErrorMessage ?? payload?.Error ?? response.ReasonPhrase ?? "Unknown error.";
            return (payload?.Error, message);
        }
        catch
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            return (null, string.IsNullOrWhiteSpace(raw) ? response.ReasonPhrase ?? "Unknown error." : raw);
        }
    }
}