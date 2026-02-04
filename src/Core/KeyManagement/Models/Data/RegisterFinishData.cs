namespace Bit.Core.KeyManagement.Models.Data;

public class RegisterFinishData
{
    public required MasterPasswordUnlockData MasterPasswordUnlockData { get; set; }
    public required UserAccountKeysData UserAccountKeysData { get; set; }
    public required MasterPasswordAuthenticationData MasterPasswordAuthenticationData { get; set; }

    public bool IsV2Encryption()
    {
        return UserAccountKeysData.IsV2Encryption();
    }

    public override bool Equals(object? obj)
    {
        if (obj is not RegisterFinishData other)
        {
            return false;
        }

        return MasterPasswordUnlockData.Equals(other.MasterPasswordUnlockData) &&
               MasterPasswordAuthenticationData.Equals(other.MasterPasswordAuthenticationData) &&
               UserAccountKeysData.Equals(other.UserAccountKeysData) &&
               IsV2Encryption() == other.IsV2Encryption();
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(MasterPasswordUnlockData, UserAccountKeysData, MasterPasswordAuthenticationData);
    }
}
