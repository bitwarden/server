using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Performs the read-only validation and policy precheck shared by the invite link confirmation
/// endpoints (retrieving the encrypted org key and confirming the user). No write operations are
/// performed; the caller is responsible for any state changes once validation succeeds.
/// </summary>
/// <remarks>
/// The following are validated:
/// <list type="bullet">
///     <item>The invite link exists and its organization is enabled and supports invite links.</item>
///     <item>The user's email domain is allowed by the link.</item>
///     <item>The user is not a provider user.</item>
///     <item>Any existing membership is neither revoked nor already confirmed.</item>
///     <item>The organization has an available seat for a new member.</item>
///     <item>The Require Two-Factor Authentication and Single Organization policies.</item>
/// </list>
/// </remarks>
public interface IConfirmOrganizationInviteLinkValidator
{
    Task<CommandResult<ConfirmOrganizationInviteLinkValidationResult>> ValidateAsync(
        ConfirmOrganizationInviteLinkValidationRequest request);
}
