namespace Bit.Core.KeyManagement.Models.Data;

public class PublicKeyEncryptionKeyPairData
{
    public string WrappedPrivateKey { get; set; }
    public string SignedPublicKey { get; set; }
    [System.Obsolete("Use SignedPublicKey instead for new code.")]
    public string PublicKey { get; set; }
}
