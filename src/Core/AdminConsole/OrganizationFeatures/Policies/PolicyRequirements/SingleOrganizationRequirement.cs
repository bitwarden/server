using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public enum SingleOrganizationRequirementResult
{
    Ok = 1,
    RequiredByThisOrganization = 2,
    RequiredByOtherOrganization = 3
}

public class SingleOrganizationRequirement : IRequirement
{
    private IEnumerable<OrganizationUserPolicyDetails> PolicyDetails { get; }

    public SingleOrganizationRequirement(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails)
    {
        PolicyDetails = userPolicyDetails
            .GetPolicyType(PolicyType.SingleOrg)
            .ExcludeOwnersAndAdmins()
            .ExcludeProviders()
            .ToList();
    }

    public SingleOrganizationRequirementResult CanJoinOrganization(Guid organizationId)
    {
        // Check for the org the user is trying to join
        if (PolicyDetails.Any(x => x.OrganizationId == organizationId))
        {
            return SingleOrganizationRequirementResult.RequiredByThisOrganization;
        }

        // Check for other orgs the user might already be a member of (accepted or confirmed status only)
        if (PolicyDetails.ExcludeRevokedAndInvitedUsers().Any())
        {
            return SingleOrganizationRequirementResult.RequiredByOtherOrganization;
        }

        return SingleOrganizationRequirementResult.Ok;
    }

    public SingleOrganizationRequirementResult CanBeRestoredToOrganization(Guid organizationId) =>
        CanJoinOrganization(organizationId);
}
