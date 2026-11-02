using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
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
///     <item><see cref="IInviteLinkValidationService.ValidateEligibility"/>.</item>
///     <item>The link supports confirmation.</item>
///     <item><see cref="InviteLinkRules.ValidateConfirmMembershipStatus"/>.</item>
///     <item><see cref="IInviteLinkValidationService.ValidateJoinRequirementsAsync"/>.</item>
///     <item><see cref="IJoinOrganizationPolicyValidationService.ValidateAccountRecoveryPolicy"/>.</item>
/// </list>
/// </remarks>
public interface IConfirmOrganizationInviteLinkValidator
{
    Task<CommandResult<ConfirmOrganizationInviteLinkValidationResult>> ValidateAsync(
        ConfirmOrganizationInviteLinkValidationRequest request);
}
