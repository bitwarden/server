#nullable enable

using Bit.Core.Auth.Entities;

namespace Bit.Core.Auth.ConnectedApps.Commands.Interfaces;

public interface IUpdateConnectedAppCommand
{
    /// <summary>
    /// Renames a user's connected app. Throws when the connection does not exist or is not owned by the
    /// user. Pushes an update to the user's other clients so the new name syncs.
    /// </summary>
    Task<ConnectedApp> UpdateAsync(Guid userId, Guid connectionId, string name);
}
