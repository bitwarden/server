using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Models.Data;

public class ProjectServiceAccountsAccessPolicies
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public IEnumerable<ServiceAccountProjectAccessPolicy> ServiceAccountProjectsAccessPolicies { get; set; }

    public IEnumerable<BaseAccessPolicy> ToBaseAccessPolicies()
    {
        var policies = new List<BaseAccessPolicy>();

        if (ServiceAccountProjectsAccessPolicies != null && ServiceAccountProjectsAccessPolicies.Any())
        {
            policies.AddRange(ServiceAccountProjectsAccessPolicies);
        }

        return policies;
    }
}
