using System.Text.Json.Serialization;

namespace AuthApi.Api.DTOs.Responses;

public sealed record KeycloakTokenResponseDto
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; init; } = string.Empty;
}