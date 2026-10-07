using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Api.KeyManagement.Models.Requests;

// This request model is meant to be used when the user will be submitting a v2 encryption WrappedAccountCryptographicState payload.
public class WrappedAccountCryptographicStateRequestModel
{
    public required PublicKeyEncryptionKeyPairRequestModel PublicKeyEncryptionKeyPair { get; set; }
    public required SignatureKeyPairRequestModel SignatureKeyPair { get; set; }
    public required SecurityStateModel SecurityState { get; set; }

    public UserAccountKeysData ToAccountKeysData()
    {
        return new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = PublicKeyEncryptionKeyPair.ToPublicKeyEncryptionKeyPairData(),
            SignatureKeyPairData = SignatureKeyPair.ToSignatureKeyPairData(),
            SecurityStateData = SecurityState.ToSecurityState()
        };
    }
}
