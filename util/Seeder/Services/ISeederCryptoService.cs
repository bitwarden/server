#nullable enable

using Bit.Core.Enums;

namespace Bit.Seeder.Services;

public interface ISeederCryptoService
{
    /// <summary>
    /// Derives a master key from password using the specified KDF
    /// </summary>
    byte[] DeriveKey(string password, string salt, KdfType kdf, int iterations);

    /// <summary>
    /// Computes the password hash for storage
    /// </summary>
    string ComputePasswordHash(byte[] masterKey, string password);

    /// <summary>
    /// Generates a new user symmetric key (64 bytes)
    /// </summary>
    byte[] GenerateUserKey();

    /// <summary>
    /// Encrypts the user key with the master key using AES-256-CBC with HMAC
    /// </summary>
    string EncryptUserKey(byte[] userKey, byte[] masterKey);

    /// <summary>
    /// Generates an RSA key pair for the user
    /// </summary>
    (string publicKey, string privateKey) GenerateUserKeyPair();

    /// <summary>
    /// Encrypts the private key with the user's symmetric key
    /// </summary>
    string EncryptPrivateKey(string privateKey, byte[] userKey);

    /// <summary>
    /// Generates a new organization symmetric key
    /// </summary>
    byte[] GenerateOrganizationKey();

    /// <summary>
    /// Encrypts text with a key (for cipher data)
    /// </summary>
    string EncryptText(string plainText, byte[] key);
}
