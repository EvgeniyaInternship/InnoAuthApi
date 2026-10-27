namespace AuthApi.Api.Constants.Keycloak;

public static class KeycloakEndpoints
{
    public static string Token(string baseUrl, string realm) =>
        $"{baseUrl.TrimEnd('/')}/realms/{realm}/protocol/openid-connect/token";

    public static string Logout(string baseUrl, string realm) =>
        $"{baseUrl.TrimEnd('/')}/realms/{realm}/protocol/openid-connect/logout";

    public static string AdminUsers(string baseUrl, string realm) =>
        $"{baseUrl.TrimEnd('/')}/admin/realms/{realm}/users";

    public static string Authority(string baseUrl, string realm) =>
        $"{baseUrl.TrimEnd('/')}/realms/{realm}";

}
