using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.KeyManagement.Models.Api.Request;

public class AccountKeysRequestModel
{
    [EncryptedString] public required string UserKeyEncryptedAccountPrivateKey { get; set; }
    public required string AccountPublicKey { get; set; }

    public PublicKeyEncryptionKeyPairRequestModel? PublicKeyEncryptionKeyPair { get; set; }
    public SignatureKeyPairRequestModel? SignatureKeyPair { get; set; }
    public SecurityStateModel? SecurityState { get; set; }

    // TODO removed with https://bitwarden.atlassian.net/browse/PM-27327
    public User ToUserV1Encryption(User existingUser)
    {
        if (string.IsNullOrWhiteSpace(AccountPublicKey) || string.IsNullOrWhiteSpace(UserKeyEncryptedAccountPrivateKey))
        {
            throw new InvalidOperationException("Public and private keys are required.");
        }

        if (string.IsNullOrWhiteSpace(existingUser.PublicKey) && string.IsNullOrWhiteSpace(existingUser.PrivateKey))
        {
            existingUser.PublicKey = AccountPublicKey;
            existingUser.PrivateKey = UserKeyEncryptedAccountPrivateKey;
            return existingUser;
        }
        else if (existingUser.PrivateKey != null &&
                 AccountPublicKey == existingUser.PublicKey &&
                 CoreHelpers.FixedTimeEquals(UserKeyEncryptedAccountPrivateKey, existingUser.PrivateKey))
        {
            return existingUser;
        }
        else
        {
            throw new InvalidOperationException("Cannot replace existing key(s) with new key(s).");
        }
    }

    public UserAccountKeysData ToAccountKeysData()
    {
        // This will be cleaned up, after a compatibility period, at which point PublicKeyEncryptionKeyPair and SignatureKeyPair will be required.
        // TODO: https://bitwarden.atlassian.net/browse/PM-23751
        if (PublicKeyEncryptionKeyPair == null)
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
            if (SignatureKeyPair == null || SecurityState == null)
            {
                return new UserAccountKeysData
                {
                    PublicKeyEncryptionKeyPairData = PublicKeyEncryptionKeyPair.ToPublicKeyEncryptionKeyPairData(),
                };
            }
            else
            {
                return new UserAccountKeysData
                {
                    PublicKeyEncryptionKeyPairData = PublicKeyEncryptionKeyPair.ToPublicKeyEncryptionKeyPairData(),
                    SignatureKeyPairData = SignatureKeyPair.ToSignatureKeyPairData(),
                    SecurityStateData = SecurityState.ToSecurityState()
                };
            }
        }
    }
}
