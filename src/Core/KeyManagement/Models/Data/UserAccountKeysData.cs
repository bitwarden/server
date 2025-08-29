namespace Bit.Core.KeyManagement.Models.Data;


public class UserAccountKeysData
{
    public required PublicKeyEncryptionKeyPairData PublicKeyEncryptionKeyPairData { get; set; }
    public SignatureKeyPairData? SignatureKeyPairData { get; set; }
    public SecurityStateData? SecurityStateData { get; set; }
}
