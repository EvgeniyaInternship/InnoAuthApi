using AuthApi.Api.DTOs.Responses;

namespace AuthApi.Api.Extensions;

public static class HttpResponseMessageExtensions
{
    public static async Task<(string? Code, string Message)> ReadKeycloakErrorAsync(
        this HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<KeycloakErrorDto>(cancellationToken: cancellationToken);
            var message = payload?.ErrorDescription
                ?? payload?.ErrorMessage
                ?? payload?.Error
                ?? response.ReasonPhrase
                ?? "Unknown error.";

            return (payload?.Error, message);
        }
        catch
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            return (null, string.IsNullOrWhiteSpace(raw) ? response.ReasonPhrase ?? "Unknown error." : raw);
        }
    }
}