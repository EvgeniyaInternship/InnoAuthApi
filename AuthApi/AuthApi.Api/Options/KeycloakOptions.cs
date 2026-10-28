namespace AuthApi.Api.Options;

public sealed class KeycloakOptions
{
    public string? BaseUrl { get; set; } 
    public string? Realm { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? RedirectUri { get; set; }
    public string? PostLoginRedirectUri { get; set; }
    public bool RequireHttpsMetadata { get; set; }
}
