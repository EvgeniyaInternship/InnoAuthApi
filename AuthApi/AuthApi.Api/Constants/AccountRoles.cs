namespace AuthApi.Api.Constants;

public static class AccountRoles
{
    public const string Admin = "Admin";
    public const string Receptionist = "Receptionist";
    public const string Doctor = "Doctor";
    public const string Patient = "Patient";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Admin,
        Receptionist,
        Doctor,
        Patient
    };
}
