using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Validates that a user is eligible to retrieve the invite for an invite link.
/// </summary>
/// <remarks>
/// The following are validated, in this order:
/// <list type="bullet">
///     <item><see cref="InviteLinkRules.ValidateEligibility"/>: the invite link exists and its code
///     matches, its organization is enabled and supports invite links, the user's email domain is allowed, and the
///     email is verified.</item>
///     <item>The existing membership's status:
///     <see cref="InviteLinkRules.ValidateConfirmMembershipStatus"/> when the link supports confirmation,
///     otherwise <see cref="InviteLinkRules.ValidateAcceptMembershipStatus"/>.</item>
///     <item>The free organization admin limit and the Automatic User Confirmation, Single Organization, and Require
///     Two-Factor Authentication policies.</item>
/// </list>
/// The Account Recovery policy requires the reset password key, so it is left to the accept and confirm endpoints.
/// </remarks>
public interface IGetOrganizationInviteValidator
{
    /// <returns>The validated invite link.</returns>
    Task<CommandResult<OrganizationInviteLink>> ValidateAsync(GetOrganizationInviteRequest request);
}
