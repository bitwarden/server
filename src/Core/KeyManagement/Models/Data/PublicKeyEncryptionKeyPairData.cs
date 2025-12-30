using System.Text.Json.Serialization;

namespace Bit.Core.KeyManagement.Models.Data;


public class PublicKeyEncryptionKeyPairData
{
    public required string WrappedPrivateKey { get; set; }
    public string? SignedPublicKey { get; set; }
    public required string PublicKey { get; set; }

    [JsonConstructor]
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute]
    public PublicKeyEncryptionKeyPairData(string wrappedPrivateKey, string publicKey, string? signedPublicKey = null)
    {
        WrappedPrivateKey = wrappedPrivateKey ?? throw new ArgumentNullException(nameof(wrappedPrivateKey));
        PublicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
        SignedPublicKey = signedPublicKey;
    }
}
