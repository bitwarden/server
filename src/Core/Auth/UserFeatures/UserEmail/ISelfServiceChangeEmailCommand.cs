using Bit.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserEmail;

public interface ISelfServiceChangeEmailCommand
{
    /// <summary>
    /// Verifies the requesting user's identity (master password + change-email token) and then
    /// delegates the email update to <see cref="IChangeEmailCommand"/>. Intended for the
    /// self-service email change flow once the master password salt has been decoupled from
    /// <see cref="User.Email"/>.
    /// </summary>
    /// <remarks>
    /// Mirrors the user-verification gates previously performed by
    /// <c>IUserService.ChangeEmailAsync</c>. The organization-domain policy gate is deliberately
    /// not duplicated here; <see cref="IChangeEmailCommand"/> enforces it as defense-in-depth.
    /// </remarks>
    /// <param name="user">The authenticated user requesting the email change.</param>
    /// <param name="masterPassword">The user's current master password hash.</param>
    /// <param name="newEmail">The new email address the user is changing to.</param>
    /// <param name="token">The change-email token previously issued for <paramref name="newEmail"/>.</param>
    Task<IdentityResult> ChangeEmailAsync(User user, string masterPassword, string newEmail, string token);
}
