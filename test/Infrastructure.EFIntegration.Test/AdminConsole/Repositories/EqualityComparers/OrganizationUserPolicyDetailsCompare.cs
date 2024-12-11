using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;

public class OrganizationUserPolicyDetailsCompare : IEqualityComparer<OrganizationUserPolicyDetails>
{
    public bool Equals(OrganizationUserPolicyDetails x, OrganizationUserPolicyDetails y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (ReferenceEquals(x, null))
        {
            return false;
        }

        if (ReferenceEquals(y, null))
        {
            return false;
        }

        if (x.GetType() != y.GetType())
        {
            return false;
        }

        return x.OrganizationId.Equals(y.OrganizationId)
            && x.PolicyType == y.PolicyType
            && x.PolicyEnabled == y.PolicyEnabled
            && x.PolicyData == y.PolicyData
            && x.OrganizationUserType == y.OrganizationUserType
            && x.OrganizationUserStatus == y.OrganizationUserStatus
            && x.OrganizationUserPermissionsData == y.OrganizationUserPermissionsData
            && x.IsProvider == y.IsProvider;
    }

    public int GetHashCode(OrganizationUserPolicyDetails obj)
    {
        return HashCode.Combine(
            obj.OrganizationId,
            (int)obj.PolicyType,
            obj.PolicyEnabled,
            obj.PolicyData,
            (int)obj.OrganizationUserType,
            (int)obj.OrganizationUserStatus,
            obj.OrganizationUserPermissionsData,
            obj.IsProvider
        );
    }
}
