namespace AuthApi.Api.DTOs.Responses;

public sealed record AuthorizationLoginDto(
    string AuthorizationUrl,
    string State,
    string CodeVerifier
);
