using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.SendFeatures.Queries.Interfaces;

/// <summary>
/// Queries sends being accessed by a recipient, rather than by their owner.
/// </summary>
public interface ISendAccessQuery
{
    /// <summary>
    /// Gets a send without regard for whether it is presently accessible.
    /// </summary>
    /// <param name="id">Identifies the send.</param>
    /// <returns>
    /// The send, or <see langword="null"/> when <paramref name="id"/> identifies no send.
    /// </returns>
    Task<Send?> Get(Guid id);
}
