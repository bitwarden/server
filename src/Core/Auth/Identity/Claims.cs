namespace Bit.Core.Auth.Identity;

public static class Claims
{
    // User
    public const string SecurityStamp = "sstamp";
    public const string Premium = "premium";
    public const string Device = "device";
    public const string DeviceType = "devicetype";

    public const string OrganizationOwner = "orgowner";
    public const string OrganizationAdmin = "orgadmin";
    public const string OrganizationUser = "orguser";
    public const string OrganizationCustom = "orgcustom";
    public const string ProviderAdmin = "providerprovideradmin";
    public const string ProviderServiceUser = "providerserviceuser";

    public const string SecretsManagerAccess = "accesssecretsmanager";

    // Service Account
    public const string Organization = "organization";

    // General
    public const string Type = "type";

    // Organization custom permissions
    public static class CustomPermissions
    {
        public const string AccessEventLogs = "accesseventlogs";
        public const string AccessImportExport = "accessimportexport";
        public const string AccessReports = "accessreports";
        public const string CreateNewCollections = "createnewcollections";
        public const string EditAnyCollection = "editanycollection";
        public const string DeleteAnyCollection = "deleteanycollection";
        public const string ManageGroups = "managegroups";
        public const string ManagePolicies = "managepolicies";
        public const string ManageSso = "managesso";
        public const string ManageUsers = "manageusers";
        public const string ManageResetPassword = "manageresetpassword";
        public const string ManageScim = "managescim";
    }

    /// <summary>
    /// The membership and authorization claim types that
    /// <see cref="Bit.Core.Utilities.CoreHelpers.BuildIdentityClaims"/> is authoritative for. These are rebuilt
    /// from the database on every token issuance, so the Identity Profile Service must not carry them over from the
    /// existing subject when refreshing a token — a refreshed token should always reflect the member's current
    /// organization and provider membership.
    /// <para>
    /// Keep this in sync with <c>BuildIdentityClaims</c>. It intentionally excludes user-identity claims
    /// (<see cref="Premium"/>, email, email_verified, name, <see cref="SecurityStamp"/>) and non-membership claims
    /// (<see cref="Type"/>, <see cref="Organization"/>, <see cref="Device"/>, <see cref="DeviceType"/>, send_*),
    /// which are overwritten or preserved separately.
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> MembershipClaimTypes = new HashSet<string>
    {
        OrganizationOwner,
        OrganizationAdmin,
        OrganizationUser,
        OrganizationCustom,
        ProviderAdmin,
        ProviderServiceUser,
        SecretsManagerAccess,
        CustomPermissions.AccessEventLogs,
        CustomPermissions.AccessImportExport,
        CustomPermissions.AccessReports,
        CustomPermissions.CreateNewCollections,
        CustomPermissions.EditAnyCollection,
        CustomPermissions.DeleteAnyCollection,
        CustomPermissions.ManageGroups,
        CustomPermissions.ManagePolicies,
        CustomPermissions.ManageSso,
        CustomPermissions.ManageUsers,
        CustomPermissions.ManageResetPassword,
        CustomPermissions.ManageScim,
    };

    public static class SendAccessClaims
    {
        public const string SendId = "send_id";
        public const string Email = "send_email";
    }
}
