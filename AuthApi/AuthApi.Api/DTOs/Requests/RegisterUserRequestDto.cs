using AuthApi.Api.Constants;
using System.Text.Json.Serialization;

namespace AuthApi.Api.DTOs.Requests;

public sealed record RegisterUserRequestDto(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    [property: JsonConverter(typeof(JsonStringEnumConverter<AccountRoles>))]
    AccountRoles Role
);
