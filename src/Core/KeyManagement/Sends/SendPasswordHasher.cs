using Bit.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.KeyManagement.Sends;

public class SendPasswordHasher(IPasswordHasher<User> passwordHasher) : ISendPasswordHasher
{
    /// <summary>
    /// Verifies an existing send password hash against a new input password hash.
    /// </summary>
    public bool VerifyPasswordHash(string sendPasswordHash, string inputPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(sendPasswordHash) || string.IsNullOrWhiteSpace(inputPasswordHash))
        {
            return false;
        }
        var passwordResult = passwordHasher.VerifyHashedPassword(new User(), sendPasswordHash, inputPasswordHash);

        return passwordResult is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }

    /// <summary>
    /// Accepts a client hashed send password and returns a server hashed password.
    /// </summary>
    public string HashPasswordHash(string clientHashedPassword)
    {
        return passwordHasher.HashPassword(new User(), clientHashedPassword);
    }
}
