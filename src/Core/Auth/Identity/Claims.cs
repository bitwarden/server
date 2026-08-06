using Duende.IdentityModel;

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
    /// The claim types the Identity Profile Service will carry over from the existing subject when refreshing a
    /// token. Everything not in this set is dropped by default — including membership and authorization claim
    /// types produced by <see cref="Bit.Core.Utilities.CoreHelpers.BuildIdentityClaims"/>, which are rebuilt from
    /// the database on every token issuance, so a refreshed token always reflects the member's current
    /// organization and provider membership rather than a stale grant of access.
    /// <para>
    /// This is an allowlist: a new claim type added to <c>BuildIdentityClaims</c> (or elsewhere) is dropped on
    /// refresh by default unless it is deliberately added here, which fails closed. Only add a claim type here if
    /// it is a user-identity claim that is safe to persist across a refresh regardless of the member's current
    /// authorization state.
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> UserIdentityClaimTypes = new HashSet<string>
    {
        // Added by Duende's GrantValidationResult when the subject is first authenticated; not produced by
        // BuildIdentityClaims, but required for the refreshed token to keep resolving to the correct principal.
        JwtClaimTypes.Subject,
        JwtClaimTypes.AuthenticationMethod,
        JwtClaimTypes.IdentityProvider,
        JwtClaimTypes.AuthenticationTime,

        // User-identity claims rebuilt by BuildIdentityClaims on every issuance; safe to carry over as a fallback
        // for issuances where the user cannot be looked up (see ProfileService.GetProfileDataAsync).
        JwtClaimTypes.Email,
        JwtClaimTypes.EmailVerified,
        JwtClaimTypes.Name,
        Premium,
        SecurityStamp,

        // Device-binding claims added when the subject is first authenticated; not produced by BuildIdentityClaims.
        Device,
        DeviceType,
    };

    public static class SendAccessClaims
    {
        public const string SendId = "send_id";
        public const string Email = "send_email";
    }
}
