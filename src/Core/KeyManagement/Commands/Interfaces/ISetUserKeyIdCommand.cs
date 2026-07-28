namespace Bit.Core.KeyManagement.Commands.Interfaces;

public interface ISetUserKeyIdCommand
{
    /// <summary>
    /// Stores the hex-encoded key id of a user's current user key.
    /// </summary>
    /// <remarks>
    /// This is a backfill primitive for accounts that pre-date the key id being reported alongside
    /// key material. It therefore only accepts a value when the account does not already have one —
    /// changing an existing key id must happen through a key rotation.
    /// </remarks>
    /// <param name="userId">The user identifier.</param>
    /// <param name="userKeyId">Hex-encoded key id of the user's current user key.</param>
    /// <exception cref="Bit.Core.Exceptions.BadRequestException">
    /// Thrown when the account already has a key id.
    /// </exception>
    Task SetUserKeyIdAsync(Guid userId, Models.Data.KeyId userKeyId);
}
