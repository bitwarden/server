using Bit.Core.SecretsManager.Entities;

namespace Bit.Seeder.Factories;

public static class AccessPolicySeeder
{
    public enum GranteeType
    {
        OrganizationUser,
        Group,
        ServiceAccount
    }

    public enum GrantableType
    {
        Project,
        ServiceAccount
    }

    public static BaseAccessPolicy Create(
        GranteeType granteeType,
        Guid granteeId,
        GrantableType grantableType,
        Guid grantableId,
        bool read,
        bool write) =>
        (granteeType, grantableType) switch
        {
            (GranteeType.OrganizationUser, GrantableType.Project) => new UserProjectAccessPolicy
            {
                OrganizationUserId = granteeId,
                GrantedProjectId = grantableId,
                Read = read,
                Write = write
            },
            (GranteeType.OrganizationUser, GrantableType.ServiceAccount) => new UserServiceAccountAccessPolicy
            {
                OrganizationUserId = granteeId,
                GrantedServiceAccountId = grantableId,
                Read = read,
                Write = write
            },
            (GranteeType.Group, GrantableType.Project) => new GroupProjectAccessPolicy
            {
                GroupId = granteeId,
                GrantedProjectId = grantableId,
                Read = read,
                Write = write
            },
            (GranteeType.Group, GrantableType.ServiceAccount) => new GroupServiceAccountAccessPolicy
            {
                GroupId = granteeId,
                GrantedServiceAccountId = grantableId,
                Read = read,
                Write = write
            },
            (GranteeType.ServiceAccount, GrantableType.Project) => new ServiceAccountProjectAccessPolicy
            {
                ServiceAccountId = granteeId,
                GrantedProjectId = grantableId,
                Read = read,
                Write = write
            },
            _ => throw new InvalidOperationException(
                $"Unsupported access policy: {granteeType} granted to {grantableType}.")
        };
}
