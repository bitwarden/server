namespace Bit.Test.Common.Constants;

public static class TestEncryptionConstants
{
    // V1-style encrypted strings (AES-CBC-HMAC formats) accepted by validators
    public const string V1EncryptedBase64 = "2.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==";

    // Public key test placeholder
    public const string PublicKey = "pk_test";

    // V2-style values used across tests
    // Private key indicating v2 (used in multiple tests to mark v2 state)
    public const string V2PrivateKey = "7.cose";
    // Wrapped signing key and verifying key values from real tests
    public const string V2WrappedSigningKey = "7.cose_signing";
    public const string V2VerifyingKey = "vk";
}


