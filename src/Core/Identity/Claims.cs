namespace Bit.Core.Identity;

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

    // Send
    public const string SendId = "send_id";
}
