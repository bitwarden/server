using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Require Two-Factor Authentication policy.
/// </summary>
public class RequireTwoFactorPolicyRequirement : IPolicyRequirement
{
    private readonly IEnumerable<PolicyDetails> _policyDetails;

    public RequireTwoFactorPolicyRequirement(IEnumerable<PolicyDetails> policyDetails)
    {
        _policyDetails = policyDetails;
    }

    /// <summary>
    /// Checks if two-factor authentication is required for the organization due to an active policy.
    /// </summary>
    /// <param name="organizationId">The ID of the organization to check.</param>
    /// <returns>True if two-factor authentication is required for the organization, false otherwise.</returns>
    /// <remarks>
    /// This does not check the user's membership status.
    /// </remarks>
    public bool IsTwoFactorRequiredForOrganization(Guid organizationId) =>
        _policyDetails.Any(p => p.OrganizationId == organizationId);

    /// <summary>
    /// Gets the active two-factor authentication policies for active memberships.
    /// </summary>
    /// <returns>The active two-factor authentication policies for active memberships.</returns>
    public IEnumerable<PolicyDetails> TwoFactorPoliciesForActiveMemberships =>
        _policyDetails.Where(p => p.OrganizationUserStatus is
                OrganizationUserStatusType.Accepted or
                OrganizationUserStatusType.Confirmed);
}

public class RequireTwoFactorPolicyRequirementFactory : BasePolicyRequirementFactory<RequireTwoFactorPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.TwoFactorAuthentication;
    protected override IEnumerable<OrganizationUserStatusType> ExemptStatuses => [];

    public override RequireTwoFactorPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        return new RequireTwoFactorPolicyRequirement(policyDetails);
    }
}
