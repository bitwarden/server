using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.TempPassword.Interfaces;

/// <summary>
/// Replaces an admin-set temporary password with a user-chosen master password. The user must
/// have <see cref="User.ForcePasswordReset"/> set. Delegates cryptographic validation to
/// <see cref="Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces.IMasterPasswordService"/>,
/// then clears the force-reset flag and notifies the user.
/// </summary>
public interface IReplaceAdminSetTemporaryPasswordCommand
{
    /// <summary>
    /// Replaces the temporary password with a new master password.
    /// </summary>
    /// <param name="user">the user replacing their temporary password</param>
    /// <param name="unlockData">new master password unlock data (encrypted user key, public/private key pair)</param>
    /// <param name="authenticationData">new master password authentication data (hash, salt, KDF configuration)</param>
    /// <param name="masterPasswordHint">optional hint for the new master password</param>
    /// <returns>success or identity errors from validation</returns>
    Task<IdentityResult> ReplaceTemporaryPasswordAsync(
        User user,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        string? masterPasswordHint);
}
