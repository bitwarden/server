using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;

public class UpdateExistingPasswordData
{
    public required MasterPasswordAuthenticationData MasterPasswordAuthentication { get; set; }
    public required MasterPasswordUnlockData MasterPasswordUnlock { get; set; }

    // Document this.
    public bool ValidatePassword { get; set; } = true;
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

        // Is this correct?
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
