namespace Bit.Core.Identity;

public static class Claims
{
    // User
    public const string SecurityStamp = "sstamp";
    public const string Premium = "premium";
    public const string Device = "device";

    public const string OrganizationOwner = "orgowner";
    public const string OrganizationAdmin = "orgadmin";
    public const string OrganizationManager = "orgmanager";
    public const string OrganizationUser = "orguser";
    public const string OrganizationCustom = "orgcustom";
    public const string ProviderAdmin = "providerprovideradmin";
    public const string ProviderServiceUser = "providerserviceuser";

    public const string SecretsManagerAccess = "accesssecretsmanager";
    public const string CreateNewCollections = "createcollect";
    public const string DeleteManagedCollections = "deletecollect";
    public const string AccessAllItems = "accessitems";

    // Service Account
    public const string Organization = "organization";

    // General
    public const string Type = "type";
}
