using Bit.Core.Models.Api;

namespace Bit.Api.KeyManagement.Models.Response;

public class UserKeysResponseModel : ResponseModel
{
    public UserKeysResponseModel(string verifyingKey, string publicKey, string publicKeyOwnershipSignature)
        : base("userKeys")
    {
        VerifyingKey = verifyingKey;
        PublicKey = publicKey;
        PublicKeyOwnershipSignature = publicKeyOwnershipSignature;
    }

    public string VerifyingKey { get; set; }
    public string PublicKey { get; set; }
    public string PublicKeyOwnershipSignature { get; set; }
}
