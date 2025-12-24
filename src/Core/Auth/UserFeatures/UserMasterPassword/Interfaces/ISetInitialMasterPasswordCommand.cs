using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;

/// <summary>
/// <para>Manages the setting of the initial master password for a <see cref="User"/> in an organization.</para>
/// <para>In organizations configured with Single Sign-On (SSO) and master password decryption:
/// just in time (JIT) provisioned users logging in via SSO are required to set a master password.</para>
/// </summary>
public interface ISetInitialMasterPasswordCommand
{
    /// <summary>
    /// Sets the initial master password and account keys for the specified user.
    /// </summary>
    /// <param name="user">User to set the master password for</param>
    /// <param name="masterPasswordDataModel">Initial master password setup data</param>
    /// <returns>A task that completes when the operation succeeds</returns>
    /// <exception cref="BadRequestException">
    /// Thrown if the user's master password is already set, the organization is not found,
    /// the user is not a member of the organization, the master password does not meet requirements.
    /// </exception>
    public Task SetInitialMasterPasswordAsync(User user, SetInitialMasterPasswordDataModel masterPasswordDataModel);
}
