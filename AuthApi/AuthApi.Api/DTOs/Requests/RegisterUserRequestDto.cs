namespace AuthApi.Api.DTOs.Requests;

public sealed record RegisterUserRequestDto(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role
);
