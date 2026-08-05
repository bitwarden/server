using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Validates that a user is eligible to accept an organization invite link.
/// </summary>
/// <remarks>
/// Owns the invite-link eligibility checks (email verified, allowed domain, provider block, existing
/// membership status, and account-recovery key requirement) and enforces the Automatic User Confirmation,
/// Single Organization, and Two-Factor Authentication policies.
///
/// For a brand-new member the target organization has no <c>OrganizationUser</c> yet, so
/// <see cref="Bit.Core.AdminConsole.OrganizationFeatures.Policies.IPolicyRequirementQuery"/> cannot resolve
/// its policies; those are read directly and paired with <c>Organization.UsePolicies</c>. For an existing
/// pending email invitation the row exists, so enforcement is delegated to the shared
/// <see cref="Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership.IAcceptOrganizationMembershipValidator"/>.
/// </remarks>
public interface IAcceptInviteLinkMembershipValidator
{
    Task<ValidationResult<AcceptInviteLinkMembershipValidationResult>> ValidateAsync(
        AcceptInviteLinkMembershipValidationRequest request);
}
