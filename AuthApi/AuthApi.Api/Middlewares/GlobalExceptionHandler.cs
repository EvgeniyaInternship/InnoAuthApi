using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AuthApi.Api.Middlewares;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            ArgumentException or ArgumentNullException =>
                (StatusCodes.Status400BadRequest, "Invalid Request"),

            UnauthorizedAccessException =>
                (StatusCodes.Status401Unauthorized, "Unauthorized"),

            KeyNotFoundException =>
                (StatusCodes.Status404NotFound, "Resource Not Found"),

            HttpRequestException httpEx =>
                ((int?)httpEx.StatusCode ?? StatusCodes.Status502BadGateway, "Identity Provider Error"),

            InvalidOperationException =>
                (StatusCodes.Status400BadRequest, "Bad Request"),

            _ =>
                (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        var path = Sanitize(httpContext.Request.Path.Value);
        var method = Sanitize(httpContext.Request.Method);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled server exception | Method: {Method} | Path: {Path} | Status: {StatusCode}",
                method,
                path,
                statusCode);
        }
        else
        {
            logger.LogWarning(
                "Client error encountered | Method: {Method} | Path: {Path} | Status: {StatusCode} | Message: {Message}",
                method,
                path,
                statusCode,
                exception.Message);
        }

        var problemDetails = new ProblemDetails
        {
            Type = exception.GetType().Name,
            Title = title,
            Status = statusCode,
            Detail = statusCode >= StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred while processing your request."
                : exception.Message,
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["traceId"] = httpContext.TraceIdentifier,
                ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            }
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await httpContext.Response.WriteAsJsonAsync(problemDetails, jsonOptions, cancellationToken);

        return true;
    }

    private static string Sanitize(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return input.Replace("\r", string.Empty).Replace("\n", string.Empty);
    }
}