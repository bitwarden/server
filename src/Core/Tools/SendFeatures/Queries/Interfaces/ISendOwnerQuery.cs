using System.Security.Claims;
using Bit.Core.Exceptions;
using Bit.Core.Tools.Entities;

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
    /// <param name="user">The principal requesting the send.</param>
    /// <returns>The send</returns>
    /// <exception cref="NotFoundException">
    /// Thrown when <paramref name="id"/> fails to identify a send
    /// owned by the user.
    /// </exception>
    /// <exception cref="BadRequestException">
    /// Thrown when the query cannot identify the current user.
    /// </exception>
    Task<Send> Get(Guid id, ClaimsPrincipal user);

    /// <summary>
    /// Gets all sends owned by the current user.
    /// </summary>
    /// <param name="user">The principal requesting the send.</param>
    /// <returns>
    /// A sequence of all owned sends.
    /// </returns>
    /// <exception cref="BadRequestException">
    /// Thrown when the query cannot identify the current user.
    /// </exception>
    Task<ICollection<Send>> GetOwned(ClaimsPrincipal user);
}
