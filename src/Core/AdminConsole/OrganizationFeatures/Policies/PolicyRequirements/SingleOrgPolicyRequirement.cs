using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Extensions;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

class SingleOrganizationPolicyRequirementFactory : IPolicyRequirementFactory<SingleOrganizationPolicyRequirement>
{
    public PolicyType Type => PolicyType.SingleOrg;

    public SingleOrganizationPolicyRequirement CreateRequirement(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails) =>
        new(userPolicyDetails.Select(up => (up.OrganizationId, up.OrganizationUserStatus)));

    public bool EnforcePolicy(OrganizationUserPolicyDetails userPolicyDetails) =>
        // Note: we include the invited status so that we can enforce this before joining an org
        !userPolicyDetails.IsAdminType();
}

class SingleOrganizationPolicyRequirement(IEnumerable<(Guid orgId, OrganizationUserStatusType status)> singleOrgOrganizations) : IPolicyRequirement
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
