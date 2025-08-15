using Bit.Core.Exceptions;
using Bit.Core.Tools.Entities;

#nullable enable

namespace Bit.Core.Tools.SendFeatures.Queries.Interfaces;

/// <summary>
/// Queries sends owned by the current user.
/// </summary>
public interface ISendOwnerQuery
{
    /// <summary>
    /// Gets a send.
    /// </summary>
    /// <param name="id">Identifies the send</param>
    /// <returns>The send</returns>
    /// <exception cref="NotFoundException">
    /// Thrown when <paramref name="id"/> fails to identify a send
    /// owned by the user.
    /// </exception>
    /// <exception cref="BadRequestException">
    /// Thrown when the query cannot identify the current user.
    /// </exception>
    Task<Send> Get(Guid id);

    /// <summary>
    /// Gets all sends owned by the current user.
    /// </summary>
    /// <returns>
    /// A sequence of all owned sends.
    /// </returns>
    /// <exception cref="BadRequestException">
    /// Thrown when the query cannot identify the current user.
    /// </exception>
    Task<ICollection<Send>> GetOwned();
}
