using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;

/// <summary>
/// Handles a user-initiated master password change, verifying the current password before
/// delegating to <see cref="IMasterPasswordService"/> for cryptographic validation and persistence.
/// </summary>
public interface ISelfServicePasswordChangeCommand
{
    /// <summary>
    /// Changes the user's master password after verifying the current password hash.
    /// </summary>
    /// <param name="user">the user changing their password</param>
    /// <param name="masterPasswordHash">current master password hash for verification</param>
    /// <param name="unlockData">new master password unlock data (encrypted user key, public/private key pair)</param>
    /// <param name="authenticationData">new master password authentication data (hash, salt, KDF configuration)</param>
    /// <param name="masterPasswordHint">optional hint for the new master password</param>
    /// <returns>success or identity errors from verification/validation</returns>
    Task<IdentityResult> ChangePasswordAsync(
        User user,
        string masterPasswordHash,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        string? masterPasswordHint);
}
