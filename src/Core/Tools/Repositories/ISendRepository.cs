#nullable enable

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
}
