using Bit.Api.SecretsManager.Utilities;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Request;

public class ServiceAccountsAccessPoliciesRequestModel
{
    public IEnumerable<AccessPolicyRequest> ProjectServiceAccountsAccessPolicyRequests { get; set; }

    public ProjectServiceAccountsAccessPolicies ToProjectServiceAccountsAccessPolicies(Guid grantedProjectId, Guid organizationId)
    {
        var projectServiceAccountsAccessPolicies = ProjectServiceAccountsAccessPolicyRequests?
            .Select(x => x.ToServiceAccountProjectAccessPolicy(grantedProjectId, organizationId)).ToList();
        var policies = new List<BaseAccessPolicy>();

        if (projectServiceAccountsAccessPolicies != null)
        {
            policies.AddRange(projectServiceAccountsAccessPolicies);
        }

        AccessPolicyHelpers.CheckForDistinctAccessPolicies(policies);
        AccessPolicyHelpers.CheckAccessPoliciesHasReadPermission(policies);

        return new ProjectServiceAccountsAccessPolicies
        {
            Id = grantedProjectId,
            OrganizationId = organizationId,
            ServiceAccountProjectsAccessPolicies = projectServiceAccountsAccessPolicies,
        };
    }
}
