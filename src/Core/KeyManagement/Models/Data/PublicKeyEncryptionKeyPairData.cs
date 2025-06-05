namespace Bit.Core.KeyManagement.Models.Data;

#nullable enable

public class PublicKeyEncryptionKeyPairData
{
    public required string WrappedPrivateKey { get; set; }
    public string? SignedPublicKey { get; set; }
    public required string PublicKey { get; set; }
}
