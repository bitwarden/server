namespace Bit.Core.KeyManagement.Sends;

public interface ISendPasswordHasher
{
    bool VerifyPasswordHash(string sendPasswordHash, string inputPasswordHash);
    string HashPasswordHash(string clientHashedPassword);
}
