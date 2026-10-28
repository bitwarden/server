using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Business rules for invite links that depend on configuration. See <see cref="InviteLinkRules"/>.
/// </summary>
public interface IInviteLinkRules
{
    /// <summary>
    /// Checks that the invite link can be used by the user, in this order:
    /// <list type="bullet">
    /// <item>The invite link exists and its code matches.</item>
    /// <item>The organization exists, is enabled, and supports invite links.</item>
    /// <item>The user's email domain is on the link's allowed domain list.</item>
    /// <item>The user's email is verified, unless a self-hosted instance has disabled this check.</item>
    /// </list>
    /// </summary>
    /// <param name="inviteLink">The organization's invite link, or <c>null</c> if it has none.</param>
    /// <param name="code">The secret code from the invite link the user is using.</param>
    /// <param name="organization">The organization the invite link belongs to, or <c>null</c> if it does not exist.</param>
    /// <param name="user">The authenticated user using the invite link.</param>
    /// <returns><c>null</c> when allowed, otherwise the error explaining why.</returns>
    Error? ValidateEligibility(
        OrganizationInviteLink? inviteLink,
        Guid code,
        Organization? organization,
        User user);
}
