using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Checks that a user has verified their email before using an invite link.
/// </summary>
public interface IInviteLinkEmailVerificationValidationService
{
    /// <summary>
    /// Checks that the user's email is verified, unless a self-hosted instance has disabled this check.
    /// </summary>
    /// <param name="user">The user using the invite link.</param>
    /// <returns><c>null</c> when allowed, otherwise the error explaining why.</returns>
    Error? ValidateEmailVerified(User user);
}
