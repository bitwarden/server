using Bit.Core.Entities;
using Bit.Core.Exceptions;


namespace Bit.Core.KeyManagement.Models.Data;

// Model represents a user key rotation request for a master password user that is also changing their master password and master password hint.
public class RotateUserAccountKeysAndChangePasswordData
{
    // Authentication for this request
    public required string OldMasterKeyAuthenticationHash { get; set; }

    public required MasterPasswordAuthenticationData MasterPasswordAuthenticationData { get; set; }
    public required MasterPasswordUnlockData MasterPasswordUnlockData { get; set; }
    public string? MasterPasswordHint { get; set; }

    public required RotateUserAccountKeysBaseData BaseData { get; set; }

    public bool ValidateForUser(User user)
    {
        //Validate salt
        MasterPasswordAuthenticationData.ValidateSaltUnchangedForUser(user);
        MasterPasswordUnlockData.ValidateSaltUnchangedForUser(user);
        if (MasterPasswordAuthenticationData.Salt != MasterPasswordUnlockData.Salt)
        {
            throw new BadRequestException("Salt for MasterPasswordAuthenticationData and MasterPasswordUnlockData must match.");
        }

        // TODO write KDF validation for the user. Must match between MasterPasswordAuthenticationData and MasterPasswordUnlockData and what is stored in the database.

        return true;
    }
}
