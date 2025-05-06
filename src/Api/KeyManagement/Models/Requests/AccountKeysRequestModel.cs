#nullable enable
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

public class AccountKeysRequestModel
{
    [EncryptedString] public required string UserKeyEncryptedAccountPrivateKey { get; set; }
    public required string AccountPublicKey { get; set; }

    public string? PublicKeyOwnershipSignature { get; set; }

    [EncryptedString] public string? UserKeyEncryptedSigningKey { get; set; }
    public string? VerifyingKey { get; set; }
    public SigningKeyType? SigningKeyType { get; set; }

    public UserAccountKeysData ToKeys()
    {
        SigningKeyData? signingKeyData = null;
        if (VerifyingKey != null && UserKeyEncryptedSigningKey != null &&
            SigningKeyType != null)
        {
            signingKeyData = new SigningKeyData
            {
                KeyAlgorithm = SigningKeyType.Value,
                WrappedSigningKey = UserKeyEncryptedSigningKey,
                VerifyingKey = VerifyingKey,
            };
        }

        return new UserAccountKeysData
        {
            AsymmetricEncryptionKeyData = new AsymmetricEncryptionKeyData
            {
                WrappedPrivateKey = UserKeyEncryptedAccountPrivateKey,
                PublicKey = AccountPublicKey,
                PublicKeyOwnershipSignature = PublicKeyOwnershipSignature,
            },
            SigningKeyData = signingKeyData,
        };
    }
}
