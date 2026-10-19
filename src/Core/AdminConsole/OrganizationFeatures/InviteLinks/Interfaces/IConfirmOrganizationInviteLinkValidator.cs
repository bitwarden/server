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
///     <item>The invite link exists, its organization is enabled and supports invite links, and the link supports confirmation.</item>
///     <item>The user has verified their email address.</item>
///     <item>The user's email domain is allowed by the link.</item>
///     <item>Any existing membership is neither revoked nor already confirmed.</item>
///     <item>The Automatic User Confirmation, Single Organization, Require Two-Factor Authentication, and Account
///     Recovery policies.</item>
/// </list>
/// </remarks>
public interface IConfirmOrganizationInviteLinkValidator
{
    Task<CommandResult<ConfirmOrganizationInviteLinkValidationResult>> ValidateAsync(
        ConfirmOrganizationInviteLinkValidationRequest request);
}
