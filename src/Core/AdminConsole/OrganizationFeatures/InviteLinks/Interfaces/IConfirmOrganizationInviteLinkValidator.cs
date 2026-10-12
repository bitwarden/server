using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
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
///     <item>The invite link exists, its organization is enabled and supports invite links, and the link supports confirmation.</item>
///     <item>The user has verified their email address.</item>
///     <item>The user's email domain is allowed by the link.</item>
///     <item>Any existing membership is neither revoked nor already confirmed.</item>
///     <item>The one-admin-per-Free-organization limit.</item>
///     <item>The organization has an available seat for a new or Staged member.</item>
///     <item><see cref="IJoinOrganizationPolicyValidationService.ValidateCanJoinAsync"/>: the Single Organization, Require Two-Factor Authentication, and
///     Automatic User Confirmation policies.</item>
/// </list>
/// The caller supplies the invite link, organization, and existing membership. Errors from
/// <see cref="IJoinOrganizationPolicyValidationService"/> are translated by <see cref="ConfirmOrganizationInviteLinkErrorMapper"/>.
/// </remarks>
public interface IConfirmOrganizationInviteLinkValidator
{
    Task<ValidationResult<ConfirmOrganizationInviteLinkValidationRequest>> ValidateAsync(
        ConfirmOrganizationInviteLinkValidationRequest request);
}
