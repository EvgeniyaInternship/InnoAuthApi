namespace AuthApi.Api.DTOs.Responses;

public sealed record AuthorizationCallbackResultDto(
    KeycloakTokenResponseDto Tokens,
    string? PostLoginRedirectUri
);
