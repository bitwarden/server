namespace Bit.Core.KeyManagement.Models.Data;

public class RegisterFinishData
{
    public required MasterPasswordUnlockData MasterPasswordUnlockData { get; set; }
    public required UserAccountKeysData UserAccountKeysData { get; set; }
    public required MasterPasswordAuthenticationData MasterPasswordAuthenticationData {get; set; }

    public bool IsV2Encryption()
    {
        return UserAccountKeysData.IsV2Encryption();
    }
}