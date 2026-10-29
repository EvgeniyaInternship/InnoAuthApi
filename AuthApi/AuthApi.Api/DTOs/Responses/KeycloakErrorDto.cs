using System.Text.Json.Serialization;

namespace AuthApi.Api.DTOs.Responses;

public sealed record KeycloakErrorDto(
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("error_description")] string? ErrorDescription,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage
);