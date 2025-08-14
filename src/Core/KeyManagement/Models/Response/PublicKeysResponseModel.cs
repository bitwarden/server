using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Models.Api;

namespace Bit.Core.KeyManagement.Models.Response;

#nullable enable

/// <summary>
/// This response model is used to return the public keys of a user, to any other registered user or entity on the server.
/// It can contain public keys (signature/encryption), and proofs between the two. It does not contain (encrypted) private keys.
/// </summary>
public class PublicKeysResponseModel : ResponseModel
{
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute]
    public PublicKeysResponseModel(UserAccountKeysData accountKeys)
        : base("publicKeys")
    {
        ArgumentNullException.ThrowIfNull(accountKeys);
        PublicKey = accountKeys.PublicKeyEncryptionKeyPairData.PublicKey;

        if (accountKeys.SignatureKeyPairData != null)
        {
            SignedPublicKey = accountKeys.PublicKeyEncryptionKeyPairData.SignedPublicKey;
            VerifyingKey = accountKeys.SignatureKeyPairData.VerifyingKey;
        }
    }

    public string? VerifyingKey { get; set; }
    public string? SignedPublicKey { get; set; }
    public required string PublicKey { get; set; }
}
