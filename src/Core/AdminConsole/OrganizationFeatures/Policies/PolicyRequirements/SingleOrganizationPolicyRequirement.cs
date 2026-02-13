using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class SingleOrganizationPolicyRequirement(IEnumerable<PolicyDetails> policyDetails) : IPolicyRequirement
{
    public record UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy() : BadRequestError(
        "Member cannot join the organization because they are in another organization which forbids it.");

    public record UserIsAMemberOfAnotherOrganizationError()
        : BadRequestError("Member cannot join the organization until they leave or remove all other organizations.");

    public record UserCannotCreateOrg()
        : BadRequestError(
            "Cannot create organization because single organization policy is enabled for another organization.");

    public Error? CanCreateOrganization() => policyDetails
        .Any(p => p.HasStatus([OrganizationUserStatusType.Accepted, OrganizationUserStatusType.Confirmed]))
        ? new UserCannotCreateOrg()
        : null;

    /// <summary>
    /// Returns an error if the user cannot join the organization.
    /// </summary>
    /// <param name="organizationId">Organization the user is attempting to join.</param>
    /// <param name="allOrgUsers">All organization users that a given user is linked to.</param>
    /// <returns>Error if the user cannot join the organization, otherwise null.</returns>
    public Error? CanJoinOrganization(Guid organizationId, ICollection<OrganizationUser> allOrgUsers) =>
        IsCompliantWithTargetOrganization(organizationId, allOrgUsers)
        ?? IsEnabledForOtherOrganizationsUserIsAPartOf(organizationId);

    /// <summary>
    /// Returns true if the policy is enabled for the target organization.
    /// </summary>
    /// <param name="targetOrganizationId">Organization Id the user is attempting to join</param>
    /// <returns></returns>
    public bool IsEnabledForTargetOrganization(Guid targetOrganizationId) =>
        policyDetails.Any(p => p.OrganizationId == targetOrganizationId);

    /// <summary>
    /// Will return an error if the user is a member of another organization and Single Organization is enabled for the
    /// target organization.
    /// </summary>
    /// <param name="targetOrganizationId">Organization Id the user is attempting to join</param>
    /// <param name="allOrgUsers">All organization users associated with the user id</param>
    /// <returns>Error if the user cannot join the target organization, otherwise null.</returns>
    public Error? IsCompliantWithTargetOrganization(Guid targetOrganizationId, ICollection<OrganizationUser> allOrgUsers) =>
        IsEnabledForTargetOrganization(targetOrganizationId)
        && allOrgUsers.Any(ou => ou.OrganizationId != targetOrganizationId)
            ? new UserIsAMemberOfAnotherOrganizationError()
            : null;

    /// <summary>
    /// Returns an error if the user is a member of another organization that has enabled the Single Organization policy.
    /// </summary>
    /// <param name="targetOrganizationId">Organization Id the user is attempting to join</param>
    /// <returns>Error if the user is a member of another organization that has enabled the Single Organization policy, otherwise null.</returns>
    public Error? IsEnabledForOtherOrganizationsUserIsAPartOf(Guid targetOrganizationId) =>
        policyDetails.Any(p => p.OrganizationId != targetOrganizationId
            && p.HasStatus([OrganizationUserStatusType.Accepted, OrganizationUserStatusType.Confirmed]))
            ? new UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy()
            : null;
}

public class
    SingleOrganizationPolicyRequirementFactory : BasePolicyRequirementFactory<SingleOrganizationPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.SingleOrg;

    protected override IEnumerable<OrganizationUserStatusType> ExemptStatuses { get; } = [];

    public override SingleOrganizationPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails) =>
        new(policyDetails);
}
