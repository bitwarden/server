namespace Bit.Core.Enums;

// If the backing type here changes to a different type you will likely also need to change the value used in
// EncryptedStringAttribute
public enum EncryptionType : byte
{
    AesCbc256_B64 = 0,
    AesCbc128_HmacSha256_B64 = 1,
    AesCbc256_HmacSha256_B64 = 2,
    Rsa2048_OaepSha256_B64 = 3,
    Rsa2048_OaepSha1_B64 = 4,
    Rsa2048_OaepSha256_HmacSha256_B64 = 5,
    Rsa2048_OaepSha1_HmacSha256_B64 = 6,
}
