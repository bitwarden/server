using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;

public class SetInitialPasswordData
{
    public required MasterPasswordAuthenticationData MasterPasswordAuthentication { get; set; }
    public required MasterPasswordUnlockData MasterPasswordUnlock { get; set; }

    /// <summary>
    /// When <c>true</c>, runs the new password hash through the registered
    /// <see cref="Microsoft.AspNetCore.Identity.IPasswordValidator{TUser}"/> pipeline before hashing.
    /// Set to <c>false</c> only in flows where password policy validation has already been enforced
    /// (e.g. admin-initiated recovery). Defaults to <c>true</c>.
    /// </summary>
    public bool ValidatePassword { get; set; } = true;
    /// <summary>
    /// When <c>true</c>, rotates <see cref="Bit.Core.Entities.User.SecurityStamp"/>, which invalidates
    /// all active sessions and authentication tokens for the user. Set to <c>false</c> only when
    /// intentionally preserving existing sessions. Defaults to <c>true</c>.
    /// </summary>
    public bool RefreshStamp { get; set; } = true;

    public string? MasterPasswordHint { get; set; } = null;

    public void ValidateDataForUser(User user)
    {
        // Validate that the user does not have a master password set.
        if (user.HasMasterPassword())
        {
            throw new BadRequestException("User already has a master password set.");
        }

        // Validate that there is no key set since there is no master password. The key
        // and MasterPassword property are siblings in that they should either both be
        // present or both be null, even for all TDE/KeyConnector users.
        if (user.Key != null)
        {
            throw new BadRequestException("User already has a key set.");
        }

        // Validate that there is no salt set.
        if (user.MasterPasswordSalt != null)
        {
            throw new BadRequestException("User already has a master password set.");
        }

        // Once a user is in the KeyConnector state they cannot become a master password
        // user ever again so we can check here to make sure that they shouldn't ever be
        // setting a password
        if (user.UsesKeyConnector)
        {
            throw new BadRequestException("Cannot set an initial password of a user with Key Connector.");
        }

        // Compatibility-window invariant: during Stage 1 of email-salt separation (PM-27044),
        // the client MUST send salt == email.lower.trim on initial SET. The server cannot yet
        // handle divergent salts; GetMasterPasswordSalt() falls back to email when MasterPasswordSalt
        // is null, and a mismatch here would make the user un-decryptable on next login. Centralized
        // here so both TDE and SSO JIT initial-SET flows enforce the same rule. This check is
        // removed in Stage 3 when PM-28143 feature flag clears and independent salts are safe.
        MasterPasswordAuthentication.ValidateSaltUnchangedForUser(user);
        MasterPasswordUnlock.ValidateSaltUnchangedForUser(user);
    }
}
