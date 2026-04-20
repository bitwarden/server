#nullable enable

using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Repositories;
using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Repositories;

/// <summary>
/// Service for saving and loading <see cref="Send"/>s in persistent storage.
/// </summary>
public interface ISendRepository : IRepository<Send, Guid>
{
    /// <summary>
    /// Loads all <see cref="Send"/>s created by a user.
    /// </summary>
    /// <param name="userId">
    /// Identifies the user.
    /// </param>
    /// <returns>
    /// A task that completes once the <see cref="Send"/>s have been loaded.
    /// The task's result contains the loaded <see cref="Send"/>s.
    /// </returns>
    Task<ICollection<Send>> GetManyByUserIdAsync(Guid userId);

    /// <summary>
    /// Loads <see cref="Send"/>s scheduled for deletion.
    /// </summary>
    /// <param name="deletionDateBefore">
    /// Load sends whose <see cref="Send.DeletionDate" /> is &lt; this date.
    /// </param>
    /// <returns>
    /// A task that completes once the <see cref="Send"/>s have been loaded.
    /// The task's result contains the loaded <see cref="Send"/>s.
    /// </returns>
    Task<ICollection<Send>> GetManyByDeletionDateAsync(DateTime deletionDateBefore);

    /// <summary>
    /// Updates encrypted data for sends during a key rotation
    /// </summary>
    /// <param name="userId">The user that initiated the key rotation</param>
    /// <param name="sends">A list of sends with updated data</param>
    UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid userId,
        IEnumerable<Send> sends);

    /// <summary>
    /// Updates the 'Disabled' field for Sends by IDs in bulk
    /// </summary>
    /// <param name="ids">A list of Send IDs to update</param>
    /// <param name="disabled">The value to set the 'Disabled' field to</param>
    Task UpdateManyDisabledAsync(IEnumerable<Guid> ids, bool disabled);

    /// <summary>
    /// Fetches the IDs of all <see cref="Send"/>ss of all Users that are members of an Organization
    /// </summary>
    /// <param name="organizationId">The ID of the organization to fetch Sends for</param>
    Task<IEnumerable<Guid>> GetIdsByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Load <see cref="Send"/>s in bulk by IDs
    /// </summary>
    /// <param name="ids">The IDs of the <see cref="Send"/>ss to load</param>
    /// <returns></returns>
    Task<ICollection<Send>> GetManyByIdsAsync(IEnumerable<Guid> ids);

    /// <summary>
    /// Update <see cref="Send"/> deletion dates in bulk by IDs
    /// </summary>
    /// <param name="ids">The IDs of the <see cref="Send"/>s to update</param>
    /// <param name="deletionHours">The number of hours after the <see cref="Send"/>s' creation dates to set the deletion date</param>
    /// <returns></returns>
    Task UpdateManyDeletionDatesByIdsAsync(IEnumerable<Guid> ids, int deletionHours);
}
