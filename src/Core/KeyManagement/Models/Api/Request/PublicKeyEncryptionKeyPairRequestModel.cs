using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.KeyManagement.Models.Api.Request;

public class PublicKeyEncryptionKeyPairRequestModel
{
    [EncryptedString] public required string WrappedPrivateKey { get; set; }
    public required string PublicKey { get; set; }
    public string? SignedPublicKey { get; set; }

    public PublicKeyEncryptionKeyPairData ToPublicKeyEncryptionKeyPairData()
    {
        return new PublicKeyEncryptionKeyPairData(
            WrappedPrivateKey,
            PublicKey,
            SignedPublicKey
        );
    }
}
