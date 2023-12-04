using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Request;

public class ServiceAccountsAccessPoliciesRequestModel
{
    public IEnumerable<AccessPolicyRequest> ProjectServiceAccountsAccessPolicyRequests { get; set; }

    private static void CheckForDistinctAccessPolicies(IReadOnlyCollection<BaseAccessPolicy> accessPolicies)
    {
        var distinctAccessPolicies = accessPolicies.DistinctBy(baseAccessPolicy =>
        {
            return baseAccessPolicy switch
            {
                ServiceAccountProjectAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.ServiceAccountId,
                    ap.GrantedProjectId),
                _ => throw new ArgumentException("Unsupported access policy type provided.", nameof(baseAccessPolicy))
            };
        }).ToList();

        if (accessPolicies.Count != distinctAccessPolicies.Count)
        {
            throw new BadRequestException("Resources must be unique");
        }
    }

    public ProjectServiceAccountsAccessPolicies ToProjectServiceAccountsAccessPolicies(Guid grantedProjectId, Guid organizationId)
    {
        var projectServiceAccountsAccessPolicies = ProjectServiceAccountsAccessPolicyRequests?
            .Select(x => x.ToServiceAccountProjectAccessPolicy(grantedProjectId, organizationId)).ToList();
        var policies = new List<BaseAccessPolicy>();

        if (projectServiceAccountsAccessPolicies != null)
        {
            policies.AddRange(projectServiceAccountsAccessPolicies);
        }

        CheckForDistinctAccessPolicies(policies);

        return new ProjectServiceAccountsAccessPolicies
        {
            Id = grantedProjectId,
            OrganizationId = organizationId,
            ServiceAccountProjectsAccessPolicies = projectServiceAccountsAccessPolicies, //TODO compare this to thomas
        };
    }
}
