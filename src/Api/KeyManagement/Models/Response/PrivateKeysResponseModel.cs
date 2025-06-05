using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Models.Api;

namespace Bit.Api.KeyManagement.Models.Response;

#nullable enable

/// <summary>
/// This response model is used to return the the asymmetric encryption keys,
/// and signature keys of an entity. This includes the private keys of the key pairs,
/// (private key, signing key), and the public keys of the key pairs (unsigned public key,
/// signed public key, verification key). 
/// </summary>
public class PrivateKeysResponseModel : ResponseModel
{
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute]
    public PrivateKeysResponseModel(UserAccountKeysData accountKeys) : base("privateKeys")
    {
        PublicKeyEncryptionKeyPair = accountKeys.PublicKeyEncryptionKeyPairData;
        if (accountKeys == null)
        {
            throw new ArgumentNullException(nameof(accountKeys));
        }

        if (accountKeys.SignatureKeyPairData != null)
        {
            SignatureKeyPair = accountKeys.SignatureKeyPairData;
        }
    }

    // Not all accounts have signature keys, but all accounts have public encryption keys.
    public SignatureKeyPairData? SignatureKeyPair { get; set; }
    public required PublicKeyEncryptionKeyPairData PublicKeyEncryptionKeyPair { get; set; }

}
