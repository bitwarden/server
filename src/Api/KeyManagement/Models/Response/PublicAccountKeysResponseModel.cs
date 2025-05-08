using Bit.Core.Models.Api;

namespace Bit.Api.KeyManagement.Models.Response;

/// <summary>
/// This response model is used to return the public keys of a user, to any other registered user or entity on the server.
/// It can contain public keys (signature/encryption), and proofs between the two. It does not contain (encrypted) private keys.
/// </summary>
public class PublicAccountKeysResponseModel : ResponseModel
{
    public PublicAccountKeysResponseModel(string verifyingKey, string publicKey, string publicKeyOwnershipSignature)
        : base("userKeys")
    {
        VerifyingKey = verifyingKey;
        PublicKey = publicKey;
        SignedPublicKeyOwnershipClaim = publicKeyOwnershipSignature;
    }

    public string VerifyingKey { get; set; }
    public string PublicKey { get; set; }
    public string SignedPublicKeyOwnershipClaim { get; set; }
}
