using AuthApi.Api.Constants.Keycloak;
using AuthApi.Api.DTOs.Requests;
using AuthApi.Api.DTOs.Responses;
using AuthApi.Api.Exceptions;
using AuthApi.Api.Extensions;
using AuthApi.Api.Interfaces;
using AuthApi.Api.Mappings;
using AuthApi.Api.Options;
using AuthApi.Api.Security;
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
            var (_, Message) = await response.ReadKeycloakErrorAsync(cancellationToken);

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

        var userId = Guid.Parse(location.Segments.Last().TrimEnd('/'));

        try
        {
            await AssignRealmRoleAsync(adminToken, userId, request.Role, cancellationToken);
        }
        catch
        {
            await DeleteUserAsync(adminToken, userId, CancellationToken.None);
            throw;
        }

        return userId;
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var adminToken = await GetServiceAccountTokenAsync(cancellationToken);
        await DeleteUserAsync(adminToken, userId, cancellationToken);
    }

    public AuthorizationLoginDto CreateLogin()
    {
        var keycloak = options.Value;
        if (string.IsNullOrWhiteSpace(keycloak.BaseUrl)
            || string.IsNullOrWhiteSpace(keycloak.Realm)
            || string.IsNullOrWhiteSpace(keycloak.ClientId)
            || string.IsNullOrWhiteSpace(keycloak.RedirectUri))
        {
            throw new InvalidOperationException("Keycloak authorization settings are incomplete.");
        }

        var state = Pkce.CreateState();
        var codeVerifier = Pkce.CreateCodeVerifier();
        var codeChallenge = Pkce.CreateCodeChallenge(codeVerifier);
        var authorizationUrl = AuthMappingExtensions.BuildAuthorizationUrl(
            KeycloakEndpoints.Authorize(keycloak.BaseUrl, keycloak.Realm),
            keycloak.ClientId,
            keycloak.RedirectUri,
            state,
            codeChallenge);

        return new AuthorizationLoginDto(authorizationUrl, state, codeVerifier);
    }

    public async Task<AuthorizationCallbackResultDto> CompleteLoginAsync(
        AuthorizationCallbackDto callback,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(callback.Error))
            throw new ArgumentException(callback.ErrorDescription ?? callback.Error);

        var redirectUri = options.Value.RedirectUri;
        if (string.IsNullOrWhiteSpace(callback.Code) || string.IsNullOrWhiteSpace(redirectUri))
            throw new ArgumentException("Authorization code is missing.");

        if (string.IsNullOrEmpty(callback.State)
            || string.IsNullOrEmpty(callback.ExpectedState)
            || !Pkce.FixedTimeEquals(callback.State, callback.ExpectedState))
        {
            throw new ArgumentException("Authorization state is invalid.");
        }

        if (string.IsNullOrWhiteSpace(callback.CodeVerifier))
            throw new ArgumentException("Authorization code verifier is missing.");

        var parameters = AuthMappingExtensions.ToAuthorizationCodeParameters(
            options.Value.ClientId,
            options.Value.ClientSecret,
            callback.Code,
            redirectUri,
            callback.CodeVerifier);
        var tokens = await RequestTokenAsync(parameters, cancellationToken);

        var postLoginRedirectUri = Uri.TryCreate(options.Value.PostLoginRedirectUri, UriKind.Absolute, out var postLoginUri)
            ? postLoginUri.ToString()
            : null;

        return new AuthorizationCallbackResultDto(tokens, postLoginRedirectUri);
    }

    public async Task<KeycloakTokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
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

    private async Task AssignRealmRoleAsync(string adminToken, Guid userId, string role, CancellationToken cancellationToken)
    {
        var roleEndpoint = KeycloakEndpoints.RealmRole(options.Value.BaseUrl, options.Value.Realm, role);
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, roleEndpoint)
        {
            Headers = { Authorization = new AuthenticationHeaderValue(KeycloakSchemes.Bearer, adminToken) }
        };

        using var getResponse = await httpClient.SendAsync(getRequest, cancellationToken);
        if (!getResponse.IsSuccessStatusCode)
        {
            var (_, message) = await getResponse.ReadKeycloakErrorAsync(cancellationToken);
            throw getResponse.StatusCode == HttpStatusCode.NotFound
                ? new ArgumentException($"Role '{role}' was not found in Keycloak.")
                : new IdentityProviderException($"Failed to resolve role '{role}': {message}");
        }

        var roleRepresentation = await getResponse.Content.ReadFromJsonAsync<KeycloakRoleDto>(cancellationToken)
            ?? throw new IdentityProviderException($"Keycloak returned an empty role payload for '{role}'.");

        var mappingEndpoint = KeycloakEndpoints.UserRealmRoleMappings(options.Value.BaseUrl, options.Value.Realm, userId);
        using var postRequest = new HttpRequestMessage(HttpMethod.Post, mappingEndpoint)
        {
            Content = JsonContent.Create(new[]
            {
                new { id = roleRepresentation.Id, name = roleRepresentation.Name }
            }),
            Headers = { Authorization = new AuthenticationHeaderValue(KeycloakSchemes.Bearer, adminToken) }
        };

        using var postResponse = await httpClient.SendAsync(postRequest, cancellationToken);
        if (!postResponse.IsSuccessStatusCode)
        {
            var (_, message) = await postResponse.ReadKeycloakErrorAsync(cancellationToken);
            throw new IdentityProviderException($"Failed to assign role '{role}': {message}");
        }
    }

    private async Task DeleteUserAsync(string adminToken, Guid userId, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            KeycloakEndpoints.AdminUser(options.Value.BaseUrl, options.Value.Realm, userId))
        {
            Headers = { Authorization = new AuthenticationHeaderValue(KeycloakSchemes.Bearer, adminToken) }
        };

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            return;

        var (_, message) = await response.ReadKeycloakErrorAsync(cancellationToken);
        throw new IdentityProviderException($"Failed to delete identity user: {message}");
    }
}