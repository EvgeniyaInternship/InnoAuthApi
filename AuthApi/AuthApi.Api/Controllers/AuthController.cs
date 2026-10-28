using AuthApi.Api.Constants.Cookie;
using AuthApi.Api.DTOs.Requests;
using AuthApi.Api.DTOs.Responses;
using AuthApi.Api.Extensions;
using AuthApi.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AuthApi.Api.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequestDto dto, CancellationToken cancellationToken)
    {
        var userId = await authService.RegisterAsync(dto, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, userId);
    }

    [HttpDelete("users/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteUser([FromRoute] Guid userId, CancellationToken cancellationToken)
    {
        await authService.DeleteUserAsync(userId, cancellationToken);
        return NoContent();
    }

    [HttpGet("login")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public IActionResult Login()
    {
        var login = authService.CreateLogin();
        Response.AppendAuthorizationFlowCookies(login.State, login.CodeVerifier);
        return Redirect(login.AuthorizationUrl);
    }

    [HttpGet("callback")]
    [ProducesResponseType(typeof(KeycloakTokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription,
        CancellationToken cancellationToken)
    {
        var result = await authService.CompleteLoginAsync(
            new AuthorizationCallbackDto(
                code,
                state,
                error,
                errorDescription,
                Request.Cookies[CookieKeys.AuthState],
                Request.Cookies[CookieKeys.CodeVerifier]),
            cancellationToken);

        Response.AppendAuthCookies(
            result.Tokens.AccessToken,
            result.Tokens.ExpiresIn,
            result.Tokens.RefreshToken,
            result.Tokens.RefreshExpiresIn);
        Response.DeleteAuthorizationFlowCookies();

        if (result.PostLoginRedirectUri is not null)
            return Redirect(result.PostLoginRedirectUri);

        return Ok(result.Tokens);
    }

    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(KeycloakTokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto dto, CancellationToken cancellationToken)
    {
        var tokens = await authService.RefreshTokenAsync(dto, cancellationToken);
        Response.AppendAuthCookies(tokens.AccessToken, tokens.ExpiresIn, tokens.RefreshToken, tokens.RefreshExpiresIn);

        return Ok(tokens);
    }

    [HttpPost("exchange-code")]
    [ProducesResponseType(typeof(KeycloakTokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExchangeCode([FromBody] ExchangeCodeRequestDto dto, CancellationToken cancellationToken)
    {
        var tokens = await authService.ExchangeCodeAsync(
            dto,
            Request.Cookies[CookieKeys.CodeVerifier],
            cancellationToken);

        Response.AppendAuthCookies(tokens.AccessToken, tokens.ExpiresIn, tokens.RefreshToken, tokens.RefreshExpiresIn);
        Response.DeleteAuthorizationFlowCookies();
        return Ok(tokens);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto dto, CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(dto, cancellationToken);
        Response.DeleteAuthCookies();
        return NoContent();
    }
}