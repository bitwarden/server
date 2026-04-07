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

        // Once a user is in the KeyConnector state they cannot become a master password user again so we can
        if (user.UsesKeyConnector)
        {
            throw new BadRequestException("Cannot set an initial password of a user with Key Connector.");
        }
    }
}
