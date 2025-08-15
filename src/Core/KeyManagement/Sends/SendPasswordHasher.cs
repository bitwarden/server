using Microsoft.AspNetCore.Identity;

namespace Bit.Core.KeyManagement.Sends;

internal class SendPasswordHasher(IPasswordHasher<SendPasswordHasherMarker> passwordHasher) : ISendPasswordHasher
{
    private readonly IPasswordHasher<SendPasswordHasherMarker> _passwordHasher = passwordHasher;

    /// <summary>
    /// <inheritdoc cref="ISendPasswordHasher.PasswordHashMatches"/>
    /// </summary>
    public bool PasswordHashMatches(string sendPasswordHash, string inputPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(sendPasswordHash) || string.IsNullOrWhiteSpace(inputPasswordHash))
        {
            return false;
        }

        var passwordResult = _passwordHasher.VerifyHashedPassword(SendPasswordHasherMarker.Instance, sendPasswordHash, inputPasswordHash);

        /*
            In our use-case we input a high-entropy, pre-hashed secret sent by the client. Thus, we don't really care
            about if the hash needs to be rehashed. Sends also only live for 30 days max.
        */
        return passwordResult is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }

    /// <summary>
    /// <inheritdoc cref="ISendPasswordHasher.HashOfClientPasswordHash"/>
    /// </summary>
    public string HashOfClientPasswordHash(string clientHashedPassword)
    {
        return _passwordHasher.HashPassword(SendPasswordHasherMarker.Instance, clientHashedPassword);
    }
}
