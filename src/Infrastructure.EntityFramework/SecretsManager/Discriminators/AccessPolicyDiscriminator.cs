namespace Bit.Infrastructure.EntityFramework.SecretsManager.Discriminators;

public static class AccessPolicyDiscriminator
{
    public const string UserProject = "user_project";
    public const string UserServiceAccount = "user_service_account";
    public const string UserSecret = "user_secret";
    public const string GroupProject = "group_project";
    public const string GroupServiceAccount = "group_service_account";
    public const string GroupSecret = "group_secret";
    public const string ServiceAccountProject = "service_account_project";
    public const string ServiceAccountSecret = "service_account_secret";
}
