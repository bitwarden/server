using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Models.Data;

public class ProjectPeopleAccessPolicies
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public IEnumerable<UserProjectAccessPolicy> UserAccessPolicies { get; set; }
    public IEnumerable<GroupProjectAccessPolicy> GroupAccessPolicies { get; set; }

    public IEnumerable<BaseAccessPolicy> ToBaseAccessPolicies()
    {
        var policies = new List<BaseAccessPolicy>();
        if (UserAccessPolicies != null && UserAccessPolicies.Any())
        {
            policies.AddRange(UserAccessPolicies);
        }

        if (GroupAccessPolicies != null && GroupAccessPolicies.Any())
        {
            policies.AddRange(GroupAccessPolicies);
        }

        return policies;
    }
}
