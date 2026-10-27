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
///     <item><see cref="InviteLinkRules.ValidateEligibility"/>.</item>
///     <item>The link supports confirmation.</item>
///     <item><see cref="InviteLinkRules.ValidateConfirmMembershipStatus"/>.</item>
///     <item>The free organization admin limit.</item>
///     <item>The Automatic User Confirmation policy.</item>
///     <item>The Single Organization policy.</item>
///     <item>The Require Two-Factor Authentication policy.</item>
///     <item>The Account Recovery policy.</item>
/// </list>
/// </remarks>
public interface IConfirmOrganizationInviteLinkValidator
{
    Task<CommandResult<ConfirmOrganizationInviteLinkValidationResult>> ValidateAsync(
        ConfirmOrganizationInviteLinkValidationRequest request);
}
