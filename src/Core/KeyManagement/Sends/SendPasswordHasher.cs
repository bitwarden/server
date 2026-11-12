using Bitwarden.Server.Sdk.PasswordHashing;

namespace Bit.Core.KeyManagement.Sends;

internal class SendPasswordHasher(IServerPasswordHasher passwordHasher) : ISendPasswordHasher
{
    private readonly IServerPasswordHasher _passwordHasher = passwordHasher;

    /// <summary>
    /// <inheritdoc cref="ISendPasswordHasher.PasswordHashMatches"/>
    /// </summary>
    public bool PasswordHashMatches(string sendPasswordHash, string inputPasswordHash)
    {
        var passwordResult = _passwordHasher.VerifyStoredHash(
            new ServerStorableHash(sendPasswordHash), new ClientCreatedHash(inputPasswordHash));

        /*
            In our use-case we input a high-entropy, pre-hashed secret sent by the client. Thus, we don't really care
            about if the hash needs to be rehashed. Sends also only live for 30 days max.
        */
        return passwordResult != PasswordHashVerification.Failed;
    }

    /// <summary>
    /// <inheritdoc cref="ISendPasswordHasher.HashOfClientPasswordHash"/>
    /// </summary>
    public string HashOfClientPasswordHash(string clientHashedPassword)
    {
        return _passwordHasher.CreateStoredHash(new ClientCreatedHash(clientHashedPassword)).Value;
    }
}
