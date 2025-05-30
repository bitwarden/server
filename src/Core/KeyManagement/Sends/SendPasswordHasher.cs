using Bit.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.KeyManagement.Sends;

public class SendPasswordHasher(IPasswordHasher<User> passwordHasher) : ISendPasswordHasher
{
    /// <summary>
    /// Verifies an existing send password hash against a new user submitted password hash.
    /// </summary>
    public bool VerifyPasswordHash(string sendPasswordHash, string userSubmittedPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(sendPasswordHash) || string.IsNullOrWhiteSpace(userSubmittedPasswordHash))
        {
            return false;
        }
        var passwordResult = passwordHasher.VerifyHashedPassword(new User(), sendPasswordHash, userSubmittedPasswordHash);

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
