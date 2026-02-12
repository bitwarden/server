using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class SingleOrganizationPolicyRequirement(IEnumerable<PolicyDetails> policyDetails) : IPolicyRequirement
{
    public record UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy() : BadRequestError("Cannot confirm this member to the organization because they are in another organization which forbids it.");

    public record UserIsAMemberOfAnotherOrganizationError() : BadRequestError("Cannot confirm this member to the organization until they leave or remove all other organizations.");

    public Error? CanJoinOrganization(Guid organizationId, OrganizationUser organizationUser) =>
        IsCompliantForOrganizationToJoin(organizationId, organizationUser)
        ?? IsEnabledForOtherOrganizationsUserIsAPartOf(organizationId);

    public bool IsEnabledForTargetOrganization(Guid targetOrganizationId) =>
        policyDetails.Any(p => p.OrganizationId == targetOrganizationId);

    public Error? IsCompliantForOrganizationToJoin(Guid targetOrganizationId, OrganizationUser organizationUser) =>
        IsEnabledForTargetOrganization(targetOrganizationId)
        && policyDetails.Single(x => x.OrganizationUserId == organizationUser.Id)
            .HasRole([OrganizationUserType.Admin, OrganizationUserType.Owner])
            ? null
            : new UserIsAMemberOfAnotherOrganizationError();

    public Error? IsEnabledForOtherOrganizationsUserIsAPartOf(Guid targetOrganizationId) =>
        policyDetails.Any(p => p.OrganizationId != targetOrganizationId)
            ? new UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy()
            : null;

    // handle all this filtering in the req

    // can return specific errors for if the org applies

    // think about who is joining and what their role is and what their current status is
}

public class SingleOrganizationPolicyRequirementFactory : BasePolicyRequirementFactory<SingleOrganizationPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.SingleOrg;

    protected override IEnumerable<OrganizationUserStatusType> ExemptStatuses { get; } = [];

    public override SingleOrganizationPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails) =>
        new(policyDetails);
}
