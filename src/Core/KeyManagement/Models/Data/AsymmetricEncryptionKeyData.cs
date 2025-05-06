namespace Bit.Core.KeyManagement.Models.Data;

public class AsymmetricEncryptionKeyData
{
    public string WrappedPrivateKey { get; set; }
    public string PublicKey { get; set; }
    public string KeyOwnershipSignature { get; set; }
}
