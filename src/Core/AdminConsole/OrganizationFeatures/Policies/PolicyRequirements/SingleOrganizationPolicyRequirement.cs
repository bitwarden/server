using Bit.Core.AdminConsole.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public enum SingleOrganizationRequirementResult
{
    Ok = 1,
    BlockedByThisOrganization = 2,
    BlockedByOtherOrganization = 3
}

public class SingleOrganizationPolicyRequirement : IPolicyRequirement
{
    public const string ErrorCannotJoinBlockedByThisOrganization = "You may not join this organization until you leave or remove all other organizations.";
    public const string ErrorCannotJoinBlockedByOtherOrganization = "You cannot join this organization because you are a member of another organization which forbids it";
    public const string ErrorCannotCreateOrganization =
        "You may not create an organization. You belong to an organization " +
        "which has a policy that prohibits you from being a member of any other organization.";

    /// <summary>
    /// Single organization policy details filtered by user role but not status.
    /// This lets us check whether a user is compliant before being accepted/restored.
    /// </summary>
    private IEnumerable<OrganizationUserPolicyDetails> PolicyDetails { get; init; }

    public static SingleOrganizationPolicyRequirement Create(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails)
        => new()
        {
            PolicyDetails = userPolicyDetails
                .GetPolicyType(PolicyType.SingleOrg)
                .ExcludeOwnersAndAdmins()
                .ExcludeProviders()
                .ToList()
        };

    public SingleOrganizationRequirementResult CanJoinOrganization(Guid organizationId)
    {
        // Check for the org the user is trying to join; status doesn't matter
        if (PolicyDetails.Any(x => x.OrganizationId == organizationId))
        {
            return SingleOrganizationRequirementResult.BlockedByThisOrganization;
        }

        // Check for other orgs the user might already be a member of (accepted or confirmed status only)
        if (PolicyDetails.ExcludeRevokedAndInvitedUsers().Any())
        {
            return SingleOrganizationRequirementResult.BlockedByOtherOrganization;
        }

        return SingleOrganizationRequirementResult.Ok;
    }

    public SingleOrganizationRequirementResult CanBeRestoredToOrganization(Guid organizationId)
        => CanJoinOrganization(organizationId);

    public bool CanCreateOrganization()
        // Check for other orgs the user might already be a member of (accepted or confirmed status only)
        => PolicyDetails.ExcludeRevokedAndInvitedUsers().Any();
}
