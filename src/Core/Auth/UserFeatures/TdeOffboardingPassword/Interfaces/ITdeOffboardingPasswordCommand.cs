using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.TdeOffboardingPassword.Interfaces;

/// <summary>
/// <para>Manages the setting of the master password for JIT provisioned TDE <see cref="User"/> in an organization, after the organization disabled TDE.</para>
/// <para>This command is invoked, when the user first logs in after the organization has switched from TDE to master password based decryption.</para>
/// </summary>
public interface ITdeOffboardingPasswordCommand
{
    [Obsolete("To be removed in PM-33141")]
    public Task<IdentityResult> UpdateTdeOffboardingPasswordAsync(User user, string masterPassword, string key,
        string? masterPasswordHint);

    /// <summary>
    /// Sets the master password for a TDE-offboarded user using structured cryptographic data via
    /// <see cref="Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces.IMasterPasswordService"/>.
    /// Clears <see cref="User.ForcePasswordReset"/> and logs the user out of all sessions.
    /// </summary>
    /// <param name="user">the TDE-offboarded user setting their master password</param>
    /// <param name="unlockData">new master password unlock data (encrypted user key, public/private key pair)</param>
    /// <param name="authenticationData">new master password authentication data (hash, salt, KDF configuration)</param>
    /// <param name="masterPasswordHint">optional hint for the new master password</param>
    /// <returns>success or identity errors from validation</returns>
    public Task<IdentityResult> UpdateTdeOffboardingPasswordAsync(User user, MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData, string? masterPasswordHint);
}
