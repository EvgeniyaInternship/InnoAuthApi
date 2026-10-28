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

    [HttpPost("login")]
    [ProducesResponseType(typeof(KeycloakTokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto, CancellationToken cancellationToken)
    {
        var tokens = await authService.LoginAsync(dto, cancellationToken);
        Response.AppendAuthCookies(tokens.AccessToken, tokens.ExpiresIn, tokens.RefreshToken, tokens.RefreshExpiresIn);

        return Ok(tokens);
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
        var tokens = await authService.ExchangeCodeAsync(dto, cancellationToken);
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