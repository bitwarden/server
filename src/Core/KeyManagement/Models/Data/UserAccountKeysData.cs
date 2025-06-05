namespace Bit.Core.KeyManagement.Models.Data;

public class UserAccountKeysData
{
    public PublicKeyEncryptionKeyPairData PublicKeyEncryptionKeyPairData { get; set; }
    public SignatureKeyPairData signatureKeyPairData { get; set; }
}
