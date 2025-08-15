using System.Text.Json.Serialization;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Models.Api;

namespace Bit.Core.KeyManagement.Models.Api.Response;


public class SignatureKeyPairResponseModel : ResponseModel
{
    public required string WrappedSigningKey { get; set; }
    public required string VerifyingKey { get; set; }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute]
    public SignatureKeyPairResponseModel(SignatureKeyPairData signatureKeyPair)
        : base("signatureKeyPair")
    {
        ArgumentNullException.ThrowIfNull(signatureKeyPair);
        WrappedSigningKey = signatureKeyPair.WrappedSigningKey;
        VerifyingKey = signatureKeyPair.VerifyingKey;
    }


    [JsonConstructor]
    public SignatureKeyPairResponseModel(string wrappedSigningKey, string verifyingKey)
        : base("signatureKeyPair")
    {
        WrappedSigningKey = wrappedSigningKey ?? throw new ArgumentNullException(nameof(wrappedSigningKey));
        VerifyingKey = verifyingKey ?? throw new ArgumentNullException(nameof(verifyingKey));
    }
}
