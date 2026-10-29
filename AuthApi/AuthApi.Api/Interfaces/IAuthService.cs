using AuthApi.Api.DTOs.Requests;
using AuthApi.Api.DTOs.Responses;

namespace AuthApi.Api.Interfaces;

public interface IAuthService
{
    Task<Guid> RegisterAsync(RegisterUserRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
    AuthorizationLoginDto CreateLogin();
    Task<AuthorizationCallbackResultDto> CompleteLoginAsync(AuthorizationCallbackDto callback, CancellationToken cancellationToken = default);
    Task<KeycloakTokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);
    Task LogoutAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);
}
