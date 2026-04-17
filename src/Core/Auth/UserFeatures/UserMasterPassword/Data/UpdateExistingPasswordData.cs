using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;

public class UpdateExistingPasswordData
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
        // Validate that the user has a master password already, if not then they shouldn't be updating they should
        // be setting initial.
        if (!user.HasMasterPassword())
        {
            throw new BadRequestException("User does not have an existing master password to update.");
        }

        // DAVE investigate if this is correct for owners and admins, can owners and admins update existing passwords
        // within the context of a key connector organization.
        if (user.UsesKeyConnector)
        {
            throw new BadRequestException("Cannot update password of a user with Key Connector.");
        }

        // Validate KDF is unchanged for user
        MasterPasswordAuthentication.Kdf.ValidateUnchangedForUser(user);
        MasterPasswordUnlock.Kdf.ValidateUnchangedForUser(user);

        // Validate Salt is unchanged for user
        MasterPasswordAuthentication.ValidateSaltUnchangedForUser(user);
        MasterPasswordUnlock.ValidateSaltUnchangedForUser(user);
    }
}
