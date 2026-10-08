using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Read-only validation that an invite link can be used by the user.
/// </summary>
/// <remarks>
/// The following are validated, in this order:
/// <list type="bullet">
///     <item>The invite link exists and its code matches.</item>
///     <item>The organization exists, is enabled, and supports invite links.</item>
///     <item>The user's email domain is on the link's allowed domain list.</item>
///     <item>The user's email is verified.</item>
/// </list>
/// The organization is read from the organization ability cache. Returns the invite link it read.
/// </remarks>
public interface IInviteLinkEligibilityValidator
{
    Task<CommandResult<OrganizationInviteLink>> ValidateAsync(InviteLinkEligibilityValidationRequest request);
}
