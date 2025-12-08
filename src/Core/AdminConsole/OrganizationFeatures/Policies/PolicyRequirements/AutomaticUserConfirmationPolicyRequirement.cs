using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Represents the enforcement status of the Automatic User Confirmation policy.
/// </summary>
/// <remarks>
/// The Automatic User Confirmation policy is enforced against all types of users regardless of status or type.
///
/// Users cannot:
/// <ul>
/// <li>Be a member of another organization (similar to Single Organization Policy)</li>
/// <li>Cannot be a provider</li>
/// </ul>
/// </remarks>
/// <param name="policyDetails">Collection of policy details that apply to this user id</param>
public class AutomaticUserConfirmationPolicyRequirement(IEnumerable<PolicyDetails> policyDetails) : IPolicyRequirement
{
    public bool CanBeGrantedEmergencyAccess() => policyDetails.Any();

    public bool CanJoinProvider() => policyDetails.Any();

    public bool CanCreateNewOrganization() => policyDetails.Any();

    public bool IsEnabled(Guid organizationId) => policyDetails.Any(p => p.OrganizationId == organizationId);

    public bool IsEnabledForOrganizationsOtherThan(Guid organizationId) =>
        policyDetails.Any(p => p.OrganizationId != organizationId);
}

public class AutomaticUserConfirmationPolicyRequirementFactory : BasePolicyRequirementFactory<AutomaticUserConfirmationPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.AutomaticUserConfirmation;

    protected override IEnumerable<OrganizationUserType> ExemptRoles => [];

    protected override IEnumerable<OrganizationUserStatusType> ExemptStatuses => [];

    protected override bool ExemptProviders => false;

    public override AutomaticUserConfirmationPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails) =>
        new(policyDetails);
}
