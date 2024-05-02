#nullable enable
using Bit.Api.SecretsManager.Utilities;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Request;

public class ServiceAccountGrantedPoliciesRequestModel
{
    public required IEnumerable<GrantedAccessPolicyRequest> ProjectGrantedPolicyRequests { get; set; }

    public ServiceAccountGrantedPolicies ToGrantedPolicies(ServiceAccount serviceAccount)
    {
        var projectGrantedPolicies = ProjectGrantedPolicyRequests
            .Select(x => x.ToServiceAccountProjectAccessPolicy(serviceAccount.Id, serviceAccount.OrganizationId))
            .ToList();

        AccessPolicyHelpers.CheckForDistinctAccessPolicies(projectGrantedPolicies);
        AccessPolicyHelpers.CheckAccessPoliciesHaveReadPermission(projectGrantedPolicies);

        return new ServiceAccountGrantedPolicies
        {
            ServiceAccountId = serviceAccount.Id,
            OrganizationId = serviceAccount.OrganizationId,
            ProjectGrantedPolicies = projectGrantedPolicies
        };
    }
}
