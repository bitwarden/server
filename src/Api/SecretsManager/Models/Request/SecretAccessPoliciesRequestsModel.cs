#nullable enable
using Bit.Api.SecretsManager.Utilities;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Request;

public class SecretAccessPoliciesRequestsModel
{
    public required IEnumerable<AccessPolicyRequest> UserAccessPolicyRequests { get; set; }

    public required IEnumerable<AccessPolicyRequest> GroupAccessPolicyRequests { get; set; }

    public required IEnumerable<AccessPolicyRequest> ServiceAccountAccessPolicyRequests { get; set; }

    public SecretAccessPolicies ToSecretAccessPolicies(Guid secretId, Guid organizationId)
    {
        var userAccessPolicies = UserAccessPolicyRequests
            .Select(x => x.ToUserSecretAccessPolicy(secretId, organizationId))
            .ToList();
        var groupAccessPolicies = GroupAccessPolicyRequests
            .Select(x => x.ToGroupSecretAccessPolicy(secretId, organizationId))
            .ToList();
        var serviceAccountAccessPolicies = ServiceAccountAccessPolicyRequests
            .Select(x => x.ToServiceAccountSecretAccessPolicy(secretId, organizationId))
            .ToList();

        var policies = new List<BaseAccessPolicy>();
        policies.AddRange(userAccessPolicies);
        policies.AddRange(groupAccessPolicies);
        policies.AddRange(serviceAccountAccessPolicies);

        AccessPolicyHelpers.CheckForDistinctAccessPolicies(policies);
        AccessPolicyHelpers.CheckAccessPoliciesHaveReadPermission(policies);

        return new SecretAccessPolicies
        {
            SecretId = secretId,
            OrganizationId = organizationId,
            UserAccessPolicies = userAccessPolicies,
            GroupAccessPolicies = groupAccessPolicies,
            ServiceAccountAccessPolicies = serviceAccountAccessPolicies,
        };
    }
}
