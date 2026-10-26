namespace AuthApi.Api.DTOs.Requests;

public sealed record LoginRequestDto(
    string Email,
    string Password
);
