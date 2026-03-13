using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Api.KeyManagement.Models.Requests;

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
