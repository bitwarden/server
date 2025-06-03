using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public enum SingleOrganizationRequirementResult
{
    /// <summary>
    /// The action is not blocked by any Single organization policy.
    /// </summary>
    Ok = 1,

    /// <summary>
    /// The action is blocked by the Single organization policy of the target organization.
    /// For example, the user cannot join the target organization because the target organization has the
    /// policy enabled, and they are a member of another organization.
    /// </summary>
    BlockedByThisOrganization = 2,

    /// <summary>
    /// The action is blocked by the Single organization policy of another organization.
    /// For example, the user cannot join the target organization because they are a member of another organization
    /// and that other organization has the policy enabled.
    /// </summary>
    BlockedByOtherOrganization = 3
}

public class SingleOrganizationPolicyRequirement(IEnumerable<PolicyDetails> singleOrganizationPolicies) : IPolicyRequirement
{
    /// <summary>
    ///
    /// </summary>
    public SingleOrganizationRequirementResult CanJoinOrganization(Guid organizationId)
    {
        // Check for the org the user is trying to join; status doesn't matter
        if (singleOrganizationPolicies.Any(x => x.OrganizationId == organizationId))
        {
            return SingleOrganizationRequirementResult.BlockedByThisOrganization;
        }

        // Check for other orgs the user might already be a member of; only enforced for accepted or confirmed
        if (singleOrganizationPolicies.Any(p =>
            p.OrganizationUserStatus is OrganizationUserStatusType.Accepted or OrganizationUserStatusType.Confirmed))
        {
            return SingleOrganizationRequirementResult.BlockedByOtherOrganization;
        }

        return SingleOrganizationRequirementResult.Ok;
    }

    public SingleOrganizationRequirementResult CanBeRestoredToOrganization(Guid organizationId)
        => CanJoinOrganization(organizationId);

    public bool CanCreateOrganization()
        => singleOrganizationPolicies.Any(p =>
            p.OrganizationUserStatus is OrganizationUserStatusType.Accepted or OrganizationUserStatusType.Confirmed);
}

public class SingleOrganizationPolicyRequirementFactory : BasePolicyRequirementFactory<SingleOrganizationPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.SingleOrg;

    // Do not exempt any statuses because we need to check this on accept/restore
    protected override IEnumerable<OrganizationUserStatusType> ExemptStatuses => [];

    public override SingleOrganizationPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
        => new(policyDetails);
}
