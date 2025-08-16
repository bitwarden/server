#nullable enable

using System.Security.Cryptography;
using System.Text;
using Bit.Core.Enums;

namespace Bit.Seeder.Services;

/// <summary>
/// Cryptographic service for seeder that generates Bitwarden-compatible encryption.
/// This implementation includes the critical HKDF expansion for master key compatibility.
/// </summary>
public class SeederCryptoService : ISeederCryptoService
{
    private const byte AesCbc256_HmacSha256_B64 = 2;

    public byte[] DeriveKey(string password, string salt, KdfType kdf, int iterations)
    {
        if (kdf == KdfType.Argon2id)
        {
            throw new NotSupportedException("Argon2id is not yet supported in the managed seeder implementation");
        }

        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var saltBytes = Encoding.UTF8.GetBytes(salt.ToLowerInvariant());

        using var pbkdf2 = new Rfc2898DeriveBytes(passwordBytes, saltBytes, iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32);
    }

    public string ComputePasswordHash(byte[] masterKey, string password)
    {
        // CRITICAL: Bitwarden's web client passes parameters in opposite order!
        // Web client: pbkdf2(salt, password, ...)
        // Standard: pbkdf2(password, salt, ...)
        // So we use masterKey as the password and password as the salt!
        using var pbkdf2 = new Rfc2898DeriveBytes(masterKey, Encoding.UTF8.GetBytes(password), 1, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(pbkdf2.GetBytes(32));
    }

    public byte[] GenerateUserKey()
    {
        // Match the web client implementation which generates two 256-bit AES keys
        // and concatenates them for a 512-bit user key

        // Generate first 256-bit AES key
        using var aes1 = Aes.Create();
        aes1.KeySize = 256;
        aes1.GenerateKey();
        var key1 = aes1.Key;

        // Generate second 256-bit AES key
        using var aes2 = Aes.Create();
        aes2.KeySize = 256;
        aes2.GenerateKey();
        var key2 = aes2.Key;

        // Concatenate to create 512-bit key (64 bytes)
        var userKey = new byte[64];
        Buffer.BlockCopy(key1, 0, userKey, 0, 32);
        Buffer.BlockCopy(key2, 0, userKey, 32, 32);

        return userKey;
    }

    public string EncryptUserKey(byte[] userKey, byte[] masterKey)
    {
        // CRITICAL: Expand master key using HKDF - this is required for Bitwarden compatibility
        var encKey = HkdfExpand(masterKey, "enc", 32);
        var macKey = HkdfExpand(masterKey, "mac", 32);

        // Combine the expanded keys
        var combinedKey = new byte[64];
        Buffer.BlockCopy(encKey, 0, combinedKey, 0, 32);
        Buffer.BlockCopy(macKey, 0, combinedKey, 32, 32);

        return AesEncrypt(userKey, combinedKey);
    }

    public (string publicKey, string privateKey) GenerateUserKeyPair()
    {
        using var rsa = RSA.Create(2048);

        // Export public key in SubjectPublicKeyInfo format
        var publicKey = rsa.ExportSubjectPublicKeyInfo();

        // Export private key in PKCS#8 format
        var privateKey = rsa.ExportPkcs8PrivateKey();

        return (Convert.ToBase64String(publicKey), Convert.ToBase64String(privateKey));
    }

    public string EncryptPrivateKey(string privateKey, byte[] userKey)
    {
        var privateKeyBytes = Convert.FromBase64String(privateKey);
        return AesEncrypt(privateKeyBytes, userKey);
    }

    public byte[] GenerateOrganizationKey()
    {
        return GenerateUserKey(); // Same structure as user key
    }

    public string EncryptText(string plainText, byte[] key)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        return AesEncrypt(plainBytes, key);
    }


    /// <summary>
    /// HKDF-Expand implementation matching Bitwarden's key derivation
    /// </summary>
    private static byte[] HkdfExpand(byte[] prk, string info, int length)
    {
        var infoBytes = Encoding.UTF8.GetBytes(info);
        var output = new byte[length];

        using var hmac = new HMACSHA256(prk);
        var counter = 1;
        var offset = 0;
        var previous = Array.Empty<byte>();

        while (offset < length)
        {
            var input = previous.Concat(infoBytes).Concat(new[] { (byte)counter }).ToArray();
            previous = hmac.ComputeHash(input);

            var copyLength = Math.Min(previous.Length, length - offset);
            Buffer.BlockCopy(previous, 0, output, offset, copyLength);

            offset += copyLength;
            counter++;
        }

        return output;
    }

    /// <summary>
    /// AES-256-CBC encryption with HMAC-SHA256 (Bitwarden Type 2 format)
    /// </summary>
    private static string AesEncrypt(byte[] data, byte[] key)
    {
        if (key.Length != 64)
        {
            throw new ArgumentException("Key must be 64 bytes (32 for encryption + 32 for MAC)");
        }

        var encKey = new byte[32];
        var macKey = new byte[32];
        Buffer.BlockCopy(key, 0, encKey, 0, 32);
        Buffer.BlockCopy(key, 32, macKey, 0, 32);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = encKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);

        // Compute MAC over encrypted data and IV
        var mac = ComputeMac(encrypted, aes.IV, macKey);

        // Type 2 format: "2.iv|data|mac"
        return $"2.{Convert.ToBase64String(aes.IV)}|{Convert.ToBase64String(encrypted)}|{Convert.ToBase64String(mac)}";
    }

    /// <summary>
    /// Computes HMAC-SHA256 for encrypted data
    /// </summary>
    private static byte[] ComputeMac(byte[] data, byte[] iv, byte[] macKey)
    {
        using var hmac = new HMACSHA256(macKey);

        // Bitwarden uses iv + encData for MAC computation (IV first!)
        var macData = new byte[iv.Length + data.Length];
        Buffer.BlockCopy(iv, 0, macData, 0, iv.Length);
        Buffer.BlockCopy(data, 0, macData, iv.Length, data.Length);

        return hmac.ComputeHash(macData);
    }
}
