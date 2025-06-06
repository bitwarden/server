#nullable enable
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Models.Data.Models;
using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

public class AccountKeysRequestModel
{
    [EncryptedString] public required string UserKeyEncryptedAccountPrivateKey { get; set; }
    public required string AccountPublicKey { get; set; }

    public PublicKeyEncryptionKeyPairRequestModel? PublicKeyEncryptionKeyPair { get; set; }
    public SignatureKeyPairRequestModel? SignatureKeyPair { get; set; }

    public UserAccountKeysData ToAccountKeysData()
    {
        // This will be cleaned up, after a compatibility period, at which point PublicKeyEncryptionKeyPair and SignatureKeyPair will be required.
        if (PublicKeyEncryptionKeyPair == null || SignatureKeyPair == null)
        {
            return new UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData
                (
                    UserKeyEncryptedAccountPrivateKey,
                    AccountPublicKey
                ),
            };
        }
        else
        {
            return new UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = PublicKeyEncryptionKeyPair.ToPublicKeyEncryptionKeyPairData(),
                SignatureKeyPairData = SignatureKeyPair.ToSignatureKeyPairData()
            };
        }
    }
}
