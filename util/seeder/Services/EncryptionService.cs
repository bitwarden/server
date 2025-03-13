using System.Security.Cryptography;
using System.Text;
using Bit.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Services;

public class EncryptionService : IEncryptionService
{
    private readonly ILogger<EncryptionService> _logger;
    private readonly IDataProtector _dataProtector;

    public EncryptionService(
        ILogger<EncryptionService> logger,
        IDataProtectionProvider dataProtectionProvider)
    {
        _logger = logger;
        _dataProtector = dataProtectionProvider.CreateProtector(Constants.DatabaseFieldProtectorPurpose);
    }

    public string HashPassword(string password)
    {
        _logger.LogDebug("Hashing password using Data Protection");

        // The real Bitwarden implementation uses BCrypt first and then protects that value
        // For simplicity we're just protecting the raw password since this is only for seeding test data
        var protectedPassword = _dataProtector.Protect(password);

        // Prefix with "P|" to match Bitwarden's password format
        return string.Concat(Constants.DatabaseFieldProtectedPrefix, protectedPassword);
    }

    public byte[] DeriveKey(string password, string salt)
    {
        _logger.LogDebug("Deriving key");

        using var pbkdf2 = new Rfc2898DeriveBytes(
            Encoding.UTF8.GetBytes(password),
            Encoding.UTF8.GetBytes(salt),
            100000,
            HashAlgorithmName.SHA256);

        return pbkdf2.GetBytes(32);
    }

    public string EncryptString(string plaintext, byte[] key)
    {
        _logger.LogDebug("Encrypting string");

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        var cipherBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }
}
