using System.Security.Claims;
using Bit.Core.Exceptions;
using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.ReceiveFeatures.Queries.Interfaces;

/// <summary>
/// Queries receives owned by the current user.
/// </summary>
public interface IReceiveOwnerQuery
{
    /// <summary>
    /// Gets a receive.
    /// </summary>
    /// <param name="id">Identifies the receive</param>
    /// <param name="user">The principal requesting the receive.</param>
    /// <returns>The receive</returns>
    /// <exception cref="NotFoundException">
    /// Thrown when <paramref name="id"/> fails to identify a receive
    /// owned by the user.
    /// </exception>
    /// <exception cref="BadRequestException">
    /// Thrown when the query cannot identify the current user.
    /// </exception>
    Task<Receive> Get(Guid id, ClaimsPrincipal user);

    /// <summary>
    /// Gets all receives owned by the current user.
    /// </summary>
    /// <param name="user">The principal requesting the receives.</param>
    /// <returns>
    /// A sequence of all owned receives.
    /// </returns>
    /// <exception cref="BadRequestException">
    /// Thrown when the query cannot identify the current user.
    /// </exception>
    Task<ICollection<Receive>> GetOwned(ClaimsPrincipal user);
}
