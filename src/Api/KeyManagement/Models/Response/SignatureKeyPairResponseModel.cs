using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Models.Api;

namespace Bit.Api.KeyManagement.Models.Response;

[method: System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute]
#nullable enable

public class SignatureKeyPairResponseModel(SignatureKeyPairData signatureKeyPair) : ResponseModel("signatureKeyPair")
{
    public required string WrappedSigningKey { get; set; } = signatureKeyPair.WrappedSigningKey;
    public required string VerifyingKey { get; set; } = signatureKeyPair.VerifyingKey;
}
