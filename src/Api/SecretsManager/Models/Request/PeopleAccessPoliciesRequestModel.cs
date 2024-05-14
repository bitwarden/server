using Bit.Api.SecretsManager.Utilities;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Request;

public class PeopleAccessPoliciesRequestModel
{
    public IEnumerable<AccessPolicyRequest> UserAccessPolicyRequests { get; set; }

    public IEnumerable<AccessPolicyRequest> GroupAccessPolicyRequests { get; set; }

    public ProjectPeopleAccessPolicies ToProjectPeopleAccessPolicies(Guid grantedProjectId, Guid organizationId)
    {
        var userAccessPolicies = UserAccessPolicyRequests?
            .Select(x => x.ToUserProjectAccessPolicy(grantedProjectId, organizationId)).ToList();

        var groupAccessPolicies = GroupAccessPolicyRequests?
            .Select(x => x.ToGroupProjectAccessPolicy(grantedProjectId, organizationId)).ToList();
        var policies = new List<BaseAccessPolicy>();
        if (userAccessPolicies != null)
        {
            policies.AddRange(userAccessPolicies);
        }

        if (groupAccessPolicies != null)
        {
            policies.AddRange(groupAccessPolicies);
        }

        AccessPolicyHelpers.CheckForDistinctAccessPolicies(policies);
        AccessPolicyHelpers.CheckAccessPoliciesHaveReadPermission(policies);

        return new ProjectPeopleAccessPolicies
        {
            Id = grantedProjectId,
            OrganizationId = organizationId,
            UserAccessPolicies = userAccessPolicies,
            GroupAccessPolicies = groupAccessPolicies
        };
    }

    public ServiceAccountPeopleAccessPolicies ToServiceAccountPeopleAccessPolicies(Guid grantedServiceAccountId,
        Guid organizationId)
    {
        var userAccessPolicies = UserAccessPolicyRequests?
            .Select(x => x.ToUserServiceAccountAccessPolicy(grantedServiceAccountId, organizationId)).ToList();

        var groupAccessPolicies = GroupAccessPolicyRequests?
            .Select(x => x.ToGroupServiceAccountAccessPolicy(grantedServiceAccountId, organizationId)).ToList();

        var policies = new List<BaseAccessPolicy>();
        if (userAccessPolicies != null)
        {
            policies.AddRange(userAccessPolicies);
        }

        if (groupAccessPolicies != null)
        {
            policies.AddRange(groupAccessPolicies);
        }

        AccessPolicyHelpers.CheckForDistinctAccessPolicies(policies);

        if (!policies.All(ap => ap.Read && ap.Write))
        {
            throw new BadRequestException("Machine account access must be Can read, write");
        }

        return new ServiceAccountPeopleAccessPolicies
        {
            Id = grantedServiceAccountId,
            OrganizationId = organizationId,
            UserAccessPolicies = userAccessPolicies,
            GroupAccessPolicies = groupAccessPolicies
        };
    }
}
