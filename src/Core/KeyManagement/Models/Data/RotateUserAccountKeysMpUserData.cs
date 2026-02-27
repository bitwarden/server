namespace Bit.Core.KeyManagement.Models.Data;

// Model represents a user key rotation request for a master password user that isn't changing their master password.
public class RotateUserAccountKeysMpUserData
{
    public required MasterPasswordUnlockData MasterPasswordUnlockData { get; set; }

    public required RotateUserAccountKeysBaseData BaseData { get; set; }
}
