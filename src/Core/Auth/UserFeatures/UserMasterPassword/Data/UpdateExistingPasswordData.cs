using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;

public class UpdateExistingPasswordData
{
    public required MasterPasswordUnlockData MasterPasswordUnlock { get; set; }
    public required MasterPasswordAuthenticationData MasterPasswordAuthentication { get; set; }

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

        // Key Connector users' encryption keys are managed by an external service, replacing the
        // master password entirely (MasterPassword is set to null on conversion). Master password
        // operations are categorically inapplicable to these users. This guard is defense-in-depth:
        // the HasMasterPassword() check above would also catch KC users, but this makes the
        // rejection reason explicit. Note: org owners/admins are structurally prohibited from
        // using Key Connector (enforced at conversion time in UserService.CheckCanUseKeyConnector),
        // so there is no owner/admin edge case to handle here.
        if (user.UsesKeyConnector)
        {
            throw new BadRequestException("Cannot update password of a user with Key Connector.");
        }

        // Validate KDF is unchanged for user
        MasterPasswordUnlock.Kdf.ValidateUnchangedForUser(user);
        MasterPasswordAuthentication.Kdf.ValidateUnchangedForUser(user);

        // Validate Salt is unchanged for user
        MasterPasswordUnlock.ValidateSaltUnchangedForUser(user);
        MasterPasswordAuthentication.ValidateSaltUnchangedForUser(user);
    }
}
