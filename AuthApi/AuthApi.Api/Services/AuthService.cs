using AuthApi.Api.Constants.Keycloak;
using AuthApi.Api.DTOs.Requests;
using AuthApi.Api.DTOs.Responses;
using AuthApi.Api.Exceptions;
using AuthApi.Api.Extensions;
using AuthApi.Api.Interfaces;
using AuthApi.Api.Mappings;
using AuthApi.Api.Options;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;

namespace AuthApi.Api.Services;

public sealed class AuthService(HttpClient httpClient, IOptions<KeycloakOptions> options) : IAuthService
{
    public async Task<Guid> RegisterAsync(RegisterUserRequestDto request, CancellationToken cancellationToken = default)
    {
        var adminToken = await GetServiceAccountTokenAsync(cancellationToken);
        var payload = request.ToKeycloakUserPayload();

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
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new InvalidOperationException("API lacks permissions in Keycloak."),
                _ => new IdentityProviderException($"Registration failed: {Message}")
            };
        }

        var location = response.Headers.Location
            ?? throw new InvalidOperationException("Keycloak response missing Location header.");

        return Guid.Parse(location.Segments.Last().TrimEnd('/'));
    }

    public async Task<KeycloakTokenResponseDto> ExchangeCodeAsync(ExchangeCodeRequestDto request, CancellationToken cancellationToken = default)
    {
        var parameters = request.ToTokenParameters(options.Value.ClientId, options.Value.ClientSecret);
        return await RequestTokenAsync(parameters, cancellationToken);
    }

    public async Task<KeycloakTokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var parameters = request.ToTokenParameters(options.Value.ClientId, options.Value.ClientSecret);
        return await RequestTokenAsync(parameters, cancellationToken);
    }

    public async Task<KeycloakTokenResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var parameters = request.ToTokenParameters(options.Value.ClientId, options.Value.ClientSecret);
        return await RequestTokenAsync(parameters, cancellationToken);
    }

    public async Task LogoutAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var parameters = request.ToLogoutParameters(options.Value.ClientId, options.Value.ClientSecret);
        var endpoint = KeycloakEndpoints.Logout(options.Value.BaseUrl, options.Value.Realm);

        using var response = await httpClient.PostAsync(endpoint, new FormUrlEncodedContent(parameters), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var (_, Message) = await response.ReadKeycloakErrorAsync(cancellationToken);
            throw new IdentityProviderException($"Logout failed: {Message}");
        }
    }

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
        var parameters = AuthMappingExtensions.ToClientCredentialsParameters(options.Value.ClientId, options.Value.ClientSecret);
        var token = await RequestTokenAsync(parameters, cancellationToken);
        return token.AccessToken;
    }
}