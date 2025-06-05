using Bit.Core.Models.Api;

namespace Bit.Api.KeyManagement.Models.Response;

/// <summary>
/// This response model is used to return the public keys of a user, to any other registered user or entity on the server.
/// It can contain public keys (signature/encryption), and proofs between the two. It does not contain (encrypted) private keys.
/// </summary>
public class PublicKeysResponseModel : ResponseModel
{
    public PublicKeysResponseModel(string verifyingKey, string publicKey, string signedPublicKey)
        : base("publicKeys")
    {
        VerifyingKey = verifyingKey;
        SignedPublicKey = signedPublicKey;
        PublicKey = publicKey;
    }

    public string VerifyingKey { get; set; }
    public string SignedPublicKey { get; set; }
    [System.Obsolete("Use SignedPublicKey for new code.")]
    public string PublicKey { get; set; }
}
