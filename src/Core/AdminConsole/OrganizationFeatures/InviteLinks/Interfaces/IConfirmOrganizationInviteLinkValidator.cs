using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Performs the read-only validation and policy precheck shared by the invite link confirmation
/// endpoints (retrieving the encrypted org key and confirming the user). No write operations are
/// performed; the caller is responsible for any state changes once validation succeeds.
/// </summary>
/// <remarks>
/// The following are validated, in this order:
/// <list type="bullet">
///     <item><see cref="IInviteLinkEligibilityValidator"/>: the invite link exists and its code matches, its
///     organization is enabled and supports invite links, the user's email domain is allowed, and the email is
///     verified.</item>
///     <item>The link supports confirmation.</item>
///     <item>Any existing membership is neither revoked nor already confirmed.</item>
///     <item><see cref="IInviteLinkOrganizationCapabilityValidator"/>: the one-admin-per-Free-organization limit, and
///     seat availability for a brand-new member.</item>
///     <item><see cref="IInviteLinkPolicyValidator"/>: the Single Organization, Require Two-Factor Authentication, and
///     Automatic User Confirmation policies.</item>
/// </list>
/// The caller supplies the invite link, organization, and existing membership. Errors from the shared validators are
/// translated by <see cref="ConfirmOrganizationInviteLinkErrorMapper"/>.
/// </remarks>
public interface IConfirmOrganizationInviteLinkValidator
{
    Task<ValidationResult<ConfirmOrganizationInviteLinkValidationRequest>> ValidateAsync(
        ConfirmOrganizationInviteLinkValidationRequest request);
}
