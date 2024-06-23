using Bit.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.Registration;

public interface IRegisterUserCommand
{

    /// <summary>
    /// Creates a new user, sends a welcome email, and raises the signup reference event.
    /// </summary>
    /// <param name="user">The <see cref="User"/> to create</param>
    /// <returns><see cref="IdentityResult"/></returns>
    public Task<IdentityResult> RegisterUser(User user);

    /// <summary>
    /// Creates a new user with a given master password hash.
    /// Optionally accepts an org invite token and org user id to associate the user with an organization upon
    /// registration and login. Both are required if either is provided.
    /// </summary>
    /// <param name="user">The <see cref="User"/> to create</param>
    /// <param name="masterPasswordHash">The hashed master password the user entered</param>
    /// <param name="orgInviteToken">The org invite token sent to them via email</param>
    /// <param name="orgUserId">The associated org user guid that was created at the time of invite</param>
    /// <returns><see cref="IdentityResult"/></returns>
    public Task<IdentityResult> RegisterUserWithOptionalOrgInvite(User user, string masterPasswordHash, string orgInviteToken, Guid? orgUserId);

    // public Task<IdentityResult> RegisterUserViaEmailVerificationToken(User user, string masterPasswordHash, string emailVerificationToken);

}
