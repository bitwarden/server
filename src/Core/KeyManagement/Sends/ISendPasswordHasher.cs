namespace Bit.Core.KeyManagement.Sends;

public interface ISendPasswordHasher
{
    bool VerifyPasswordHash(string sendPasswordHash, string userSubmittedPasswordHash);
    string HashPasswordHash(string clientHashedPassword);
}
