namespace AuthApi.Api.Constants.Keycloak;

public static class KeycloakEndpoints
{
    public static string Authorize(string baseUrl, string realm) =>
        $"{baseUrl.TrimEnd('/')}/realms/{realm}/protocol/openid-connect/auth";

    public static string Token(string baseUrl, string realm) =>
        $"{baseUrl.TrimEnd('/')}/realms/{realm}/protocol/openid-connect/token";

    public static string Logout(string baseUrl, string realm) =>
        $"{baseUrl.TrimEnd('/')}/realms/{realm}/protocol/openid-connect/logout";

    public static string AdminUsers(string baseUrl, string realm) =>
        $"{baseUrl.TrimEnd('/')}/admin/realms/{realm}/users";

    public static string AdminUser(string baseUrl, string realm, Guid userId) =>
        $"{AdminUsers(baseUrl, realm)}/{userId}";

    public static string RealmRole(string baseUrl, string realm, string roleName) =>
        $"{baseUrl.TrimEnd('/')}/admin/realms/{realm}/roles/{Uri.EscapeDataString(roleName)}";

    public static string UserRealmRoleMappings(string baseUrl, string realm, Guid userId) =>
        $"{AdminUser(baseUrl, realm, userId)}/role-mappings/realm";

    public static string Authority(string baseUrl, string realm) =>
        $"{baseUrl.TrimEnd('/')}/realms/{realm}";

}
