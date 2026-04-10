using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.KeyManagement.Models.Api.Request;

public class SignatureKeyPairRequestModel
{
    public required string SignatureAlgorithm { get; set; }
    [EncryptedString] public required string WrappedSigningKey { get; set; }
    public required string VerifyingKey { get; set; }

    public SignatureKeyPairData ToSignatureKeyPairData()
    {
        if (SignatureAlgorithm != "ed25519" && SignatureAlgorithm != "mldsa65")
        {
            throw new ArgumentException(
                $"Unsupported signature algorithm: {SignatureAlgorithm}"
            );
        }
        var algorithm = SignatureAlgorithm == "ed25519"
            ? Core.KeyManagement.Enums.SignatureAlgorithm.Ed25519
            : Core.KeyManagement.Enums.SignatureAlgorithm.MlDsa65;

        return new SignatureKeyPairData(
            algorithm,
            WrappedSigningKey,
            VerifyingKey
        );
    }
}
