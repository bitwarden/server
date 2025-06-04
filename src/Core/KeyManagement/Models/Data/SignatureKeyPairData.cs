#nullable enable

using Bit.Core.Enums;

namespace Bit.Core.KeyManagement.Models.Data;

public class SignatureKeyPairData
{
    public required SignatureAlgorithm SignatureAlgorithm { get; set; }
    public required string WrappedSigningKey { get; set; }
    public required string VerifyingKey { get; set; }
}
