using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response;

/// <summary>
/// This response model is used to return keys of a user - downstream of the user key - to the client.
/// This includes the private keys (signature/encryption), and proof tying one to another. This could
/// also be used to contain further user-owned keys in the future (per-vault keys, etc). This should
/// not be used to contain keys not just owned by the user (e.g. organization keys).
/// </summary>
public class PrivateAccountKeysResponseModel : ResponseModel
{
    public PrivateAccountKeysResponseModel(UserAccountKeysData accountKeys) : base("accountKeys")
    {
        if (accountKeys != null)
        {
            SignatureKeyPair = accountKeys.signatureKeyPairData;
        }
        PublicKeyEncryptionKeyPair = accountKeys.PublicKeyEncryptionKeyPairData;
    }

    public PrivateAccountKeysResponseModel() : base("accountKeys")
    {
    }

    public SignatureKeyPairData SignatureKeyPair { get; set; }
    public PublicKeyEncryptionKeyPairData PublicKeyEncryptionKeyPair { get; set; }

}
