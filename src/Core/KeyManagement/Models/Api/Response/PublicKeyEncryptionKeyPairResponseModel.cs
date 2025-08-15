using System.Text.Json.Serialization;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Models.Api;

namespace Bit.Core.KeyManagement.Models.Api.Response;


public class PublicKeyEncryptionKeyPairResponseModel : ResponseModel
{
    public required string WrappedPrivateKey { get; set; }
    public required string PublicKey { get; set; }
    public string? SignedPublicKey { get; set; }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute]
    public PublicKeyEncryptionKeyPairResponseModel(PublicKeyEncryptionKeyPairData keyPair)
        : base("publicKeyEncryptionKeyPair")
    {
        WrappedPrivateKey = keyPair.WrappedPrivateKey;
        PublicKey = keyPair.PublicKey;
        SignedPublicKey = keyPair.SignedPublicKey;
    }

    [JsonConstructor]
    public PublicKeyEncryptionKeyPairResponseModel(string wrappedPrivateKey, string publicKey, string? signedPublicKey)
        : base("publicKeyEncryptionKeyPair")
    {
        WrappedPrivateKey = wrappedPrivateKey ?? throw new ArgumentNullException(nameof(wrappedPrivateKey));
        PublicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
        SignedPublicKey = signedPublicKey;
    }
}
