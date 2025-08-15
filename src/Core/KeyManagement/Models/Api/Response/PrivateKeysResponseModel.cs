using System.Text.Json.Serialization;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Models.Api;

namespace Bit.Core.KeyManagement.Models.Api.Response;


/// <summary>
/// This response model is used to return the asymmetric encryption keys,
/// and signature keys of an entity. This includes the private keys of the key pairs,
/// (private key, signing key), and the public keys of the key pairs (unsigned public key,
/// signed public key, verification key). 
/// </summary>
public class PrivateKeysResponseModel : ResponseModel
{
    // Not all accounts have signature keys, but all accounts have public encryption keys.
    public SignatureKeyPairResponseModel? SignatureKeyPair { get; set; }
    public required PublicKeyEncryptionKeyPairResponseModel PublicKeyEncryptionKeyPair { get; set; }
    public SecurityStateModel? SecurityState { get; set; }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute]
    public PrivateKeysResponseModel(UserAccountKeysData accountKeys) : base("privateKeys")
    {
        PublicKeyEncryptionKeyPair = new PublicKeyEncryptionKeyPairResponseModel(accountKeys.PublicKeyEncryptionKeyPairData);
        ArgumentNullException.ThrowIfNull(accountKeys);

        if (accountKeys.SignatureKeyPairData != null && accountKeys.SecurityStateData != null)
        {
            SignatureKeyPair = new SignatureKeyPairResponseModel(accountKeys.SignatureKeyPairData);
            SecurityState = SecurityStateModel.FromSecurityStateData(accountKeys.SecurityStateData!);
        }
    }

    [JsonConstructor]
    public PrivateKeysResponseModel(SignatureKeyPairResponseModel? signatureKeyPair, PublicKeyEncryptionKeyPairResponseModel publicKeyEncryptionKeyPair, SecurityStateModel? securityState)
        : base("privateKeys")
    {
        SignatureKeyPair = signatureKeyPair;
        PublicKeyEncryptionKeyPair = publicKeyEncryptionKeyPair ?? throw new ArgumentNullException(nameof(publicKeyEncryptionKeyPair));
        SecurityState = securityState;
    }
}
