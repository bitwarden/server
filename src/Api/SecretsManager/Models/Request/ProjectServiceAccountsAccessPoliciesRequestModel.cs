#nullable enable
using Bit.Api.SecretsManager.Utilities;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Request;

public class ProjectServiceAccountsAccessPoliciesRequestModel
{
    public required IEnumerable<AccessPolicyRequest> ServiceAccountAccessPolicyRequests { get; set; }

    public ProjectServiceAccountsAccessPolicies ToProjectServiceAccountsAccessPolicies(Project project)
    {
        var serviceAccountAccessPolicies = ServiceAccountAccessPolicyRequests
            .Select(x => x.ToServiceAccountProjectAccessPolicy(project.Id, project.OrganizationId))
            .ToList();

        AccessPolicyHelpers.CheckForDistinctAccessPolicies(serviceAccountAccessPolicies);
        AccessPolicyHelpers.CheckAccessPoliciesHaveReadPermission(serviceAccountAccessPolicies);

        return new ProjectServiceAccountsAccessPolicies
        {
            ProjectId = project.Id,
            OrganizationId = project.OrganizationId,
            ServiceAccountAccessPolicies = serviceAccountAccessPolicies
        };
    }
}
