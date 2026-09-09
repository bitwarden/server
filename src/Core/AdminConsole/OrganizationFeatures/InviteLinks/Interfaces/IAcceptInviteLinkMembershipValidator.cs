using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Validates that a user is eligible to accept an organization invite link.
/// </summary>
/// <remarks>
/// Owns the invite-link eligibility checks (email verified, allowed domain, existing membership status, free
/// organization admin limit, and account-recovery key requirement) and enforces the Automatic User
/// Confirmation, Single Organization, and Two-Factor Authentication policies. A single path handles every
/// accept case (brand-new member, Staged provisioning, or existing email invitation): the target org's
/// policies are read directly (they cannot be resolved through
/// <see cref="Bit.Core.AdminConsole.OrganizationFeatures.Policies.IPolicyRequirementQuery"/> until a
/// resolvable membership row exists), while the user's other organizations are still evaluated through the
/// requirement framework.
///
/// Returns the validated <see cref="AcceptInviteLinkMembershipValidationRequest"/> so callers act on the same
/// request they supplied.
/// </remarks>
public interface IAcceptInviteLinkMembershipValidator
{
    Task<ValidationResult<AcceptInviteLinkMembershipValidationRequest>> ValidateAsync(
        AcceptInviteLinkMembershipValidationRequest request);
}
