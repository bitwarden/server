using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Repositories;
using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Repositories;

/// <summary>
/// Service for saving and loading <see cref="Receive"/>s in persistent storage.
/// </summary>
public interface IReceiveRepository : IRepository<Receive, Guid>
{
    /// <summary>
    /// Loads all <see cref="Receive"/>s created by a user.
    /// </summary>
    /// <param name="userId">
    /// Identifies the user.
    /// </param>
    /// <returns>
    /// A task that completes once the <see cref="Receive"/>s have been loaded.
    /// The task's result contains the loaded <see cref="Receive"/>s.
    /// </returns>
    Task<ICollection<Receive>> GetManyByUserIdAsync(Guid userId);

    /// <summary>
    /// Loads <see cref="Receive"/>s whose expiration date has passed.
    /// </summary>
    /// <param name="expirationDate">
    /// Load receives whose <see cref="Receive.ExpirationDate" /> is &lt; this date.
    /// </param>
    /// <returns>
    /// A task that completes once the <see cref="Receive"/>s have been loaded.
    /// The task's result contains the loaded <see cref="Receive"/>s.
    /// </returns>
    Task<ICollection<Receive>> GetManyByExpirationDateAsync(DateTime expirationDate);

    /// <summary>
    /// Updates encrypted data for receives during a key rotation
    /// </summary>
    /// <param name="userId">The user that initiated the key rotation</param>
    /// <param name="receives">A list of receives with updated data</param>
    UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid userId,
        IEnumerable<Receive> receives);
}
