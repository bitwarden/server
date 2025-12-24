using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Core.Auth.UserFeatures.TdeOnboardingPassword.Interfaces;

/// <summary>
/// <para>Manages the setting of the master password onboarding for a TDE <see cref="User"/> in an organization.</para>
/// <para>In organizations configured with SSO and trusted devices decryption:
/// Users who are upgraded to have admin account recovery permissions must set a master password
/// to ensure their ability to reset other users' accounts.</para>
/// </summary>
public interface ITdeOnboardingPasswordCommand
{
    /// <summary>
    /// Onboard the master password for the specified TDE user.
    /// </summary>
    /// <param name="user">User to set the master password for</param>
    /// <param name="masterPasswordDataModel">Master password setup data</param>
    /// <returns>A task that completes when the operation succeeds</returns>
    /// <exception cref="BadRequestException">
    /// Thrown if the user's master password is already set, the organization is not found,
    /// the user is not a member of the organization, the master password does not meet requirements,
    /// or the user is a TDE user without account keys set.
    /// </exception>
    Task OnboardMasterPasswordAsync(User user, TdeOnboardMasterPasswordDataModel masterPasswordDataModel);
}
