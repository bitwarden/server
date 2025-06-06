#nullable enable
using Bit.Core.KeyManagement.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

public class SignatureKeyPairRequestModel
{
    public required SignatureAlgorithm SignatureAlgorithm { get; set; }
    [EncryptedString] public required string WrappedSigningKey { get; set; }
    public required string VerifyingKey { get; set; }

    public SignatureKeyPairData ToSignatureKeyPairData()
    {
        return new SignatureKeyPairData(
            SignatureAlgorithm,
            WrappedSigningKey,
            VerifyingKey
        );
    }
}
