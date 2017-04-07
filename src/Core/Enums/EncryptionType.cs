namespace Bit.Core.Enums
{
    public enum EncryptionType : byte
    {
        AesCbc256_B64 = 0,
        AesCbc128_HmacSha256_B64 = 1,
        AesCbc256_HmacSha256_B64 = 2,
        RsaOaep_Sha256_B64 = 3
    }
}
