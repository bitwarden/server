
using System.Text.Json.Serialization;
using Bit.Core.KeyManagement.Enums;

namespace Bit.Core.KeyManagement.Models.Data;

public class SignatureKeyPairData
{
    public required SignatureAlgorithm SignatureAlgorithm { get; set; }
    public required string WrappedSigningKey { get; set; }
    public required string VerifyingKey { get; set; }

    [JsonConstructor]
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute]
    public SignatureKeyPairData(SignatureAlgorithm signatureAlgorithm, string wrappedSigningKey, string verifyingKey)
    {
        SignatureAlgorithm = signatureAlgorithm;
        WrappedSigningKey = wrappedSigningKey ?? throw new ArgumentNullException(nameof(wrappedSigningKey));
        VerifyingKey = verifyingKey ?? throw new ArgumentNullException(nameof(verifyingKey));
    }

    public override bool Equals(object? obj)
    {
        if (obj is not SignatureKeyPairData other)
        {
            return false;
        }

        return SignatureAlgorithm == other.SignatureAlgorithm &&
               WrappedSigningKey == other.WrappedSigningKey &&
               VerifyingKey == other.VerifyingKey;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(SignatureAlgorithm, WrappedSigningKey, VerifyingKey);
    }
}
