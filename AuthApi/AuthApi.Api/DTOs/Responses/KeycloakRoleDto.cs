using System.Text.Json.Serialization;

namespace AuthApi.Api.DTOs.Responses;

public sealed record KeycloakRoleDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name
);
