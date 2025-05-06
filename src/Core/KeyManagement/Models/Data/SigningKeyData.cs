using Bit.Core.Enums;

namespace Bit.Core.KeyManagement.Models.Data;

public class SigningKeyData
{
    public SigningKeyType KeyAlgorithm { get; set; }
    public string WrappedSigningKey { get; set; }
    public string VerifyingKey { get; set; }
}
