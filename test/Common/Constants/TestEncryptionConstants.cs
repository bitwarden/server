namespace Bit.Test.Common.Constants;

public static class TestEncryptionConstants
{
    // Intended for use as a V1 encrypted string, accepted by validators
    public const string AES256_CBC_HMAC_Encstring = "2.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==";

    // Public key test placeholder
    public const string PublicKey = "pk_test";

    // V2-style values used across tests
    // Private key indicating v2 (used in multiple tests to mark v2 state)
    public const string V2PrivateKey = "7.cose";
    // Wrapped signing key and verifying key values from real tests
    public const string V2WrappedSigningKey = "7.cose_signing";
    public const string V2VerifyingKey = "vk";
}
