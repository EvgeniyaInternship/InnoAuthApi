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

public sealed class AuthService(
    HttpClient httpClient,
    IOptions<KeycloakOptions> options,
    ILogger<AuthService> logger) : IAuthService
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
            var (_, message) = await response.ReadKeycloakErrorAsync(cancellationToken);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                logger.LogWarning("User with email {Email} already exists.", request.Email);
                throw new ConflictException($"User with email '{request.Email}' already exists.");
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                logger.LogWarning("User registration failed: {Reason}.", message);
                throw new ArgumentException($"User registration failed: {message}");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                logger.LogError("API lacks permissions in Keycloak.");
                throw new InvalidOperationException("API lacks permissions in Keycloak.");
            }

            logger.LogError("Registration failed: {Reason}.", message);
            throw new IdentityProviderException($"Registration failed: {message}");
        }

        if (response.Headers.Location is null)
        {
            logger.LogError("Keycloak response missing Location header.");
            throw new InvalidOperationException("Keycloak response missing Location header.");
        }

        var userId = Guid.Parse(response.Headers.Location.Segments.Last().TrimEnd('/'));

        try
        {
            await AssignRealmRoleAsync(adminToken, userId, request.Role.ToString(), cancellationToken);
        }
        catch
        {
            await DeleteUserAsync(adminToken, userId, CancellationToken.None);
            throw;
        }

        logger.LogInformation("Registered user {UserId} with role {Role}.", userId, request.Role);
        return userId;
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var adminToken = await GetServiceAccountTokenAsync(cancellationToken);
        var deleted = await DeleteUserAsync(adminToken, userId, cancellationToken);
        if (deleted)
            logger.LogInformation("Deleted user {UserId}.", userId);
    }

    public AuthorizationLoginDto CreateLogin()
    {
        var keycloak = options.Value;
        if (string.IsNullOrWhiteSpace(keycloak.BaseUrl)
            || string.IsNullOrWhiteSpace(keycloak.Realm)
            || string.IsNullOrWhiteSpace(keycloak.ClientId)
            || string.IsNullOrWhiteSpace(keycloak.RedirectUri))
        {
            logger.LogError("Keycloak authorization settings are incomplete.");
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

        logger.LogInformation("Login started.");
        return new AuthorizationLoginDto(authorizationUrl, state, codeVerifier);
    }

    public async Task<AuthorizationCallbackResultDto> CompleteLoginAsync(
        AuthorizationCallbackDto callback,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(callback.Error))
        {
            logger.LogWarning("Login was rejected: {Reason}.", callback.ErrorDescription ?? callback.Error);
            throw new ArgumentException(callback.ErrorDescription ?? callback.Error);
        }

        var redirectUri = options.Value.RedirectUri;
        if (string.IsNullOrWhiteSpace(callback.Code) || string.IsNullOrWhiteSpace(redirectUri))
        {
            logger.LogWarning("Authorization code is missing.");
            throw new ArgumentException("Authorization code is missing.");
        }

        if (string.IsNullOrEmpty(callback.State)
            || string.IsNullOrEmpty(callback.ExpectedState)
            || !Pkce.FixedTimeEquals(callback.State, callback.ExpectedState))
        {
            logger.LogWarning("Authorization state is invalid.");
            throw new ArgumentException("Authorization state is invalid.");
        }

        if (string.IsNullOrWhiteSpace(callback.CodeVerifier))
        {
            logger.LogWarning("Authorization code verifier is missing.");
            throw new ArgumentException("Authorization code verifier is missing.");
        }

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

        logger.LogInformation("Login callback completed.");
        return new AuthorizationCallbackResultDto(tokens, postLoginRedirectUri);
    }

    public async Task<KeycloakTokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var parameters = request.ToTokenParameters(options.Value.ClientId, options.Value.ClientSecret);
        var tokens = await RequestTokenAsync(parameters, cancellationToken);
        logger.LogInformation("Refresh token succeeded.");
        return tokens;
    }

    public async Task LogoutAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var parameters = request.ToLogoutParameters(options.Value.ClientId, options.Value.ClientSecret);
        var endpoint = KeycloakEndpoints.Logout(options.Value.BaseUrl, options.Value.Realm);

        using var response = await httpClient.PostAsync(endpoint, new FormUrlEncodedContent(parameters), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var (_, message) = await response.ReadKeycloakErrorAsync(cancellationToken);
            logger.LogWarning("Logout failed: {Reason}.", message);
            throw new IdentityProviderException($"Logout failed: {message}");
        }

        logger.LogInformation("Logout completed.");
    }

    private async Task<KeycloakTokenResponseDto> RequestTokenAsync(Dictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var endpoint = KeycloakEndpoints.Token(options.Value.BaseUrl, options.Value.Realm);
        using var response = await httpClient.PostAsync(endpoint, new FormUrlEncodedContent(parameters), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var (code, message) = await response.ReadKeycloakErrorAsync(cancellationToken);
            if (string.Equals(code, "invalid_grant", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Token request was rejected: {Reason}.", message);
                throw new InvalidGrantException(message);
            }

            logger.LogError("Token request failed: {Reason}.", message);
            throw new IdentityProviderException(message);
        }

        var tokens = await response.Content.ReadFromJsonAsync<KeycloakTokenResponseDto>(cancellationToken: cancellationToken);
        if (tokens is null)
        {
            logger.LogError("Keycloak returned an empty token payload.");
            throw new InvalidOperationException("Keycloak returned an empty token payload.");
        }

        return tokens;
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
            if (getResponse.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogWarning("Role {Role} does not exist in Keycloak.", role);
                throw new ArgumentException($"Role '{role}' was not found in Keycloak.");
            }

            logger.LogError("Failed to resolve role {Role}: {Reason}.", role, message);
            throw new IdentityProviderException($"Failed to resolve role '{role}': {message}");
        }

        var roleRepresentation = await getResponse.Content.ReadFromJsonAsync<KeycloakRoleDto>(cancellationToken);
        if (roleRepresentation is null)
        {
            logger.LogError("Keycloak returned an empty role payload for {Role}.", role);
            throw new IdentityProviderException($"Keycloak returned an empty role payload for '{role}'.");
        }

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
            logger.LogError("Failed to assign role {Role} to user {UserId}: {Reason}.", role, userId, message);
            throw new IdentityProviderException($"Failed to assign role '{role}': {message}");
        }
    }

    private async Task<bool> DeleteUserAsync(string adminToken, Guid userId, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            KeycloakEndpoints.AdminUser(options.Value.BaseUrl, options.Value.Realm, userId))
        {
            Headers = { Authorization = new AuthenticationHeaderValue(KeycloakSchemes.Bearer, adminToken) }
        };

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogWarning("User with id {UserId} does not exist.", userId);
            return false;
        }

        if (response.IsSuccessStatusCode)
            return true;

        var (_, message) = await response.ReadKeycloakErrorAsync(cancellationToken);
        logger.LogError("Failed to delete user {UserId}: {Reason}.", userId, message);
        throw new IdentityProviderException($"Failed to delete identity user: {message}");
    }
}