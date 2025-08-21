namespace Bit.Core.KeyManagement.Sends;

public interface ISendPasswordHasher
{
    /// <summary>
    /// Matches the send password hash against the user provided client password hash. The send password is server hashed and the client
    /// password hash is hashed by the server for comparison <see cref="HashOfClientPasswordHash"/> in this method.
    /// </summary>
    /// <param name="sendPasswordHash">The send password that is hashed by the server.</param>
    /// <param name="clientPasswordHash">The user provided password hash that has not yet been hashed by the server for comparison.</param>
    /// <returns>true if hashes match false otherwise</returns>
    bool PasswordHashMatches(string sendPasswordHash, string clientPasswordHash);

    /// <summary>
    /// Accepts a client hashed send password and returns a server hashed password.
    /// </summary>
    /// <param name="clientHashedPassword"></param>
    /// <returns>server hashed password</returns>
    string HashOfClientPasswordHash(string clientHashedPassword);
}
