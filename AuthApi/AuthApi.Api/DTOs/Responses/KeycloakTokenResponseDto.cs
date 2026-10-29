using System.Text.Json.Serialization;

namespace AuthApi.Api.DTOs.Responses;

public sealed record KeycloakTokenResponseDto(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("refresh_expires_in")] int RefreshExpiresIn
);