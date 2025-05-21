using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Enums;

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
    /// Determines if the user can accept an invitation to an organization.
    /// </summary>
    /// <param name="twoFactorEnabled">Whether the user has two-step login enabled.</param>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <returns>True if the user can accept the invitation, false otherwise.</returns>
    public bool CanAcceptInvitation(bool twoFactorEnabled, Guid organizationId) =>
        twoFactorEnabled ||
        !_policyDetails.Any(p => p.OrganizationId == organizationId &&
            (p.OrganizationUserStatus is
                OrganizationUserStatusType.Invited or
                OrganizationUserStatusType.Accepted or
                OrganizationUserStatusType.Confirmed));


    /// <summary>
    /// Determines if the user can be confirmed in an organization.
    /// </summary>
    /// <param name="twoFactorEnabled">Whether the user has two-step login enabled.</param>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <returns>True if the user can be confirmed, false otherwise.</returns>
    public bool CanBeConfirmed(bool twoFactorEnabled, Guid organizationId) =>
        twoFactorEnabled ||
        !_policyDetails.Any(p => p.OrganizationId == organizationId &&
            (p.OrganizationUserStatus is
                OrganizationUserStatusType.Accepted or
                OrganizationUserStatusType.Confirmed));


    /// <summary>
    /// Determines if the user can be restored in an organization.
    /// </summary>
    /// <param name="twoFactorEnabled">Whether the user has two-step login enabled.</param>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <returns>True if the user can be restored, false otherwise.</returns>
    public bool CanBeRestored(bool twoFactorEnabled, Guid organizationId) =>
        twoFactorEnabled ||
        !_policyDetails.Any(p => p.OrganizationId == organizationId &&
            (p.OrganizationUserStatus is
                OrganizationUserStatusType.Revoked or
                OrganizationUserStatusType.Invited or
                OrganizationUserStatusType.Accepted or
                OrganizationUserStatusType.Confirmed));

    /// <summary>
    /// Gets the two-factor policies for active memberships.
    /// </summary>
    /// <returns>The two-factor policies for active memberships.</returns>
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
