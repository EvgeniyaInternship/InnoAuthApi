namespace AuthApi.Api.DTOs.Requests;

public sealed record ExchangeCodeRequestDto(
    string Code,
    string RedirectUri
);
