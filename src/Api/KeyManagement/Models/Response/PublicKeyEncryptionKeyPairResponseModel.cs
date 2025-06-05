using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Models.Api;

namespace Bit.Api.KeyManagement.Models.Response;

[method: System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute]
#nullable enable

public class PublicKeyEncryptionKeyPairModel(PublicKeyEncryptionKeyPairData keyPair) : ResponseModel("publicKeyEncryptionKeyPair")
{
    public required string WrappedPrivateKey { get; set; } = keyPair.WrappedPrivateKey;
    public required string PublicKey { get; set; } = keyPair.PublicKey;
    public string? SignedPublicKey { get; set; } = keyPair.SignedPublicKey;
}
