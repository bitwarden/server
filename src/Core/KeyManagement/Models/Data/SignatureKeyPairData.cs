#nullable enable
using Bit.Core.Enums;

namespace Bit.Core.KeyManagement.Models.Data;

public class SignatureKeyPairData
{
    required public SignatureAlgorithm SignatureAlgorithm { get; set; }
    required public string WrappedSigningKey { get; set; }
    required public string VerifyingKey { get; set; }
}
