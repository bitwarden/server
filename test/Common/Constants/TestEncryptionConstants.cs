namespace Bit.Test.Common.Constants;

public static class TestEncryptionConstants
{

    // Simple stubs for different encrypted string versions
    public const string AES256_CBC_B64_Encstring = "0.stub";
    public const string AES128_CBC_HMACSHA256_B64_Encstring = "1.stub";
    public const string AES256_CBC_HMAC_EmptySuffix = "2.";
    // Intended for use as a V1 encrypted string, accepted by validators
    public const string AES256_CBC_HMAC_Encstring = "2.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==";
    public const string RSA2048_OAEPSHA256_B64_Encstring = "3.stub";
    public const string RSA2048_OAEPSHA1_B64_Encstring = "4.stub";
    public const string RSA2048_OAEPSHA256_HMACSHA256_B64_Encstring = "5.stub";
    public const string RSA2048_OAEPSHA1_HMACSHA256_B64_Encstring = "6.stub";
    public const string XCHACHA20POLY1305_B64_Encstring = "7.stub";

    // Public key test placeholder
    public const string PublicKey = "pk_test";

    // V2-style values used across tests
    // Private key indicating v2 (used in multiple tests to mark v2 state)
    public const string V2PrivateKey = "7.cose";
    // Wrapped signing key and verifying key values from real tests
    public const string V2WrappedSigningKey = "7.cose_signing";
    public const string V2VerifyingKey = "vk";
}
