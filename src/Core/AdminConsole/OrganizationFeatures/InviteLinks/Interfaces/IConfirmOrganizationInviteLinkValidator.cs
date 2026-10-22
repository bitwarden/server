using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Performs the read-only validation and policy precheck shared by the invite link confirmation
/// endpoints (retrieving the encrypted org key and confirming the user). No write operations are
/// performed; the caller is responsible for any state changes once validation succeeds.
/// </summary>
/// <remarks>
/// See <see cref="BaseInviteLinkMembershipValidator"/> for the checks performed. In addition, the link must
/// support confirmation, and an existing membership must be neither revoked nor already confirmed.
/// </remarks>
public interface IConfirmOrganizationInviteLinkValidator
{
    Task<CommandResult<InviteLinkMembershipValidationResult>> ValidateAsync(
        ConfirmOrganizationInviteLinkValidationRequest request);
}
