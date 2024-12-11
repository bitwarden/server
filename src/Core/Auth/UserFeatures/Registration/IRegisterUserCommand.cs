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
    /// Creates a new user with a given master password hash, sends a welcome email (differs based on initiation path),
    /// and raises the signup reference event. Optionally accepts an org invite token and org user id to associate
    /// the user with an organization upon registration and login. Both are required if either is provided or validation will fail.
    /// If the organization has a 2FA required policy enabled, email verification will be enabled for the user.
    /// </summary>
    /// <param name="user">The <see cref="User"/> to create</param>
    /// <param name="masterPasswordHash">The hashed master password the user entered</param>
    /// <param name="orgInviteToken">The org invite token sent to the user via email</param>
    /// <param name="orgUserId">The associated org user guid that was created at the time of invite</param>
    /// <returns><see cref="IdentityResult"/></returns>
    public Task<IdentityResult> RegisterUserViaOrganizationInviteToken(
        User user,
        string masterPasswordHash,
        string orgInviteToken,
        Guid? orgUserId
    );

    /// <summary>
    /// Creates a new user with a given master password hash, sends a welcome email, and raises the signup reference event.
    /// If a valid email verification token is provided, the user will be created with their email verified.
    /// An error will be thrown if the token is invalid or expired.
    /// </summary>
    /// <param name="user">The <see cref="User"/> to create</param>
    /// <param name="masterPasswordHash">The hashed master password the user entered</param>
    /// <param name="emailVerificationToken">The email verification token sent to the user via email</param>
    /// <returns><see cref="IdentityResult"/></returns>
    public Task<IdentityResult> RegisterUserViaEmailVerificationToken(
        User user,
        string masterPasswordHash,
        string emailVerificationToken
    );

    /// <summary>
    /// Creates a new user with a given master password hash, sends a welcome email, and raises the signup reference event.
    /// If a valid org sponsored free family plan invite token is provided, the user will be created with their email verified.
    /// If the token is invalid or expired, an error will be thrown.
    /// </summary>
    /// <param name="user">The <see cref="User"/> to create</param>
    /// <param name="masterPasswordHash">The hashed master password the user entered</param>
    /// <param name="orgSponsoredFreeFamilyPlanInviteToken">The org sponsored free family plan invite token sent to the user via email</param>
    /// <returns><see cref="IdentityResult"/></returns>
    public Task<IdentityResult> RegisterUserViaOrganizationSponsoredFreeFamilyPlanInviteToken(
        User user,
        string masterPasswordHash,
        string orgSponsoredFreeFamilyPlanInviteToken
    );

    /// <summary>
    /// Creates a new user with a given master password hash, sends a welcome email, and raises the signup reference event.
    /// If a valid token is provided, the user will be created with their email verified.
    /// If the token is invalid or expired, an error will be thrown.
    /// </summary>
    /// <param name="user">The <see cref="User"/> to create</param>
    /// <param name="masterPasswordHash">The hashed master password the user entered</param>
    /// <param name="acceptEmergencyAccessInviteToken">The emergency access invite token sent to the user via email</param>
    /// <param name="acceptEmergencyAccessId">The emergency access id (used to validate the token)</param>
    /// <returns><see cref="IdentityResult"/></returns>
    public Task<IdentityResult> RegisterUserViaAcceptEmergencyAccessInviteToken(
        User user,
        string masterPasswordHash,
        string acceptEmergencyAccessInviteToken,
        Guid acceptEmergencyAccessId
    );

    /// <summary>
    /// Creates a new user with a given master password hash, sends a welcome email, and raises the signup reference event.
    /// If a valid token is provided, the user will be created with their email verified.
    /// If the token is invalid or expired, an error will be thrown.
    /// </summary>
    /// <param name="user">The <see cref="User"/> to create</param>
    /// <param name="masterPasswordHash">The hashed master password the user entered</param>
    /// <param name="providerInviteToken">The provider invite token sent to the user via email</param>
    /// <param name="providerUserId">The provider user id which is used to validate the invite token</param>
    /// <returns><see cref="IdentityResult"/></returns>
    public Task<IdentityResult> RegisterUserViaProviderInviteToken(
        User user,
        string masterPasswordHash,
        string providerInviteToken,
        Guid providerUserId
    );
}
