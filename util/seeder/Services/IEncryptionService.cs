namespace Bit.Seeder.Services;

public interface IEncryptionService
{
    string HashPassword(string password);
    byte[] DeriveKey(string password, string salt);
    string EncryptString(string plaintext, byte[] key);
} 