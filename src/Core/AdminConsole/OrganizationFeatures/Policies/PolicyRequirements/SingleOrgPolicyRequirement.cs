using Bit.Core.AdminConsole.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

class SingleOrganizationPolicyRequirementDefinition : IPolicyRequirementDefinition<SingleOrganizationPolicyRequirement>
{
    public PolicyType Type => PolicyType.SingleOrg;

    public SingleOrganizationPolicyRequirement Reduce(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails) =>
        new(userPolicyDetails.Select(up => (up.OrganizationId, up.OrganizationUserStatus)));

    public bool FilterPredicate(OrganizationUserPolicyDetails userPolicyDetails) =>
        // Note: we include the invited status so that we can enforce this before joining an org
        userPolicyDetails.OrganizationUserType is not (OrganizationUserType.Owner or OrganizationUserType.Admin) &&
        !userPolicyDetails.IsProvider;
}

class SingleOrganizationPolicyRequirement(IEnumerable<(Guid orgId, OrganizationUserStatusType status)> singleOrgOrganizations) : IPolicyRequirement
{
    /// <summary>
    /// Returns true only for Accepted and Invited users, which replicates the legacy behavior.
    /// To enforce this policy before the user is allowed to join an organization, use CanJoinOrganization instead.
    /// </summary>
    public bool AppliesToUser => singleOrgOrganizations.Any(x =>
        x.status is OrganizationUserStatusType.Accepted or OrganizationUserStatusType.Confirmed);
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
