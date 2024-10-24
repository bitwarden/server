using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IPolicyRequirement;

public interface IPolicyRequirementDefinition<T> where T : IPolicyRequirement
{
    PolicyType Type { get; }
    T Reduce(IEnumerable<Policy> policies);
    bool FilterPredicate(Policy policy);
}

public record DisableSendPolicyRequirement(bool DisableSend) : IPolicyRequirement;

public class SingleOrganizationPolicyRequirement (IEnumerable<(Guid orgId, OrganizationUserStatusType status)> singleOrgOrganizations) : IPolicyRequirement
{
    public string CanJoinOrganization(Guid organizationId)
    {
        // Check for the org the user is trying to join
        if (singleOrgOrganizations.Any(x => x.orgId == organizationId))
        {
            return "error";
        }

        // Check for other orgs the user might already be a member of (accepted or confirmed status only)
        if (singleOrgOrganizations.Any(x =>
                x.status is OrganizationUserStatusType.Accepted or OrganizationUserStatusType.Confirmed))
        {
            return "error";
        }

        return "";
    }

    public string CanBeRestoredToOrganization(Guid organizationId) => CanJoinOrganization(organizationId);
}
