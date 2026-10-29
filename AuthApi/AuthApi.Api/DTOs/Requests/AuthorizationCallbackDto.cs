namespace AuthApi.Api.DTOs.Requests;

public sealed record AuthorizationCallbackDto(
    string? Code,
    string? State,
    string? Error,
    string? ErrorDescription,
    string? ExpectedState,
    string? CodeVerifier
);
