using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;

public class UpdateExistingPasswordData
{
    public required MasterPasswordAuthenticationData MasterPasswordAuthenticationData { get; set; }
    public required MasterPasswordUnlockData MasterPasswordUnlockData { get; set; }
    public bool ValidatePassword { get; set; } = true;
    public bool RefreshStamp { get; set; } = true;

    public void ValidateDataForUser(User user)
    {
        // Validate that the user has a master password already, if not then they shouldn't be updating they should
        // be setting initial.
        if (!user.HasMasterPassword())
        {
            throw new BadRequestException("User does not have an existing master password to update.");
        }

        // Validate KDF is unchanged for user
        MasterPasswordAuthenticationData.Kdf.ValidateUnchangedForUser(user);
        MasterPasswordUnlockData.Kdf.ValidateUnchangedForUser(user);

        // Validate Salt is unchanged for user
        MasterPasswordAuthenticationData.ValidateSaltUnchangedForUser(user);
        MasterPasswordUnlockData.ValidateSaltUnchangedForUser(user);
    }
}
