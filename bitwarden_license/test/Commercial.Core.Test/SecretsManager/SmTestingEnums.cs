namespace Bit.Commercial.Core.Test.SecretsManager;

public enum TestAccessPolicyType
{
    UserProjectAccessPolicy,
    GroupProjectAccessPolicy,
    ServiceAccountProjectAccessPolicy,
    UserServiceAccountAccessPolicy,
    GroupServiceAccountAccessPolicy,

}

public enum TestPermissionType
{
    RunAsAdmin,
    RunAsUserWithPermission,
}
