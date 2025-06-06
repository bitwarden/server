namespace Bit.Core.KeyManagement.Models.Data.Models;

#nullable enable

public class UserAccountKeysData
{
    public required PublicKeyEncryptionKeyPairData PublicKeyEncryptionKeyPairData { get; set; }
    public SignatureKeyPairData? SignatureKeyPairData { get; set; }
}
