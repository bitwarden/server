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
    /// This should be used to check whether the member needs to have 2FA enabled before being
    /// accepted, confirmed, or restored to the organization.
    /// </remarks>
    public bool IsTwoFactorRequiredForOrganization(Guid organizationId) =>
        _policyDetails.Any(p => p.OrganizationId == organizationId);

    /// <summary>
    /// Returns tuples of (OrganizationId, OrganizationUserId) for active memberships where two-factor authentication is required.
    /// Users should be revoked from these organizations if they disable all 2FA methods.
    /// </summary>
    public IEnumerable<(Guid OrganizationId, Guid OrganizationUserId)> OrganizationsRequiringTwoFactor =>
        _policyDetails
            .Where(p => p.OrganizationUserStatus is
                OrganizationUserStatusType.Accepted or
                OrganizationUserStatusType.Confirmed)
            .Select(p => (p.OrganizationId, p.OrganizationUserId));
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
