using System.Security.Claims;
using Bit.Core.Exceptions;

namespace Bit.Core.Tools.ReceiveFeatures.Queries.Interfaces;

/// <summary>
/// Gets a download URL for a specific file in a Receive, verifying ownership.
/// </summary>
public interface IGetReceiveFileDownloadQuery
{
    /// <summary>
    /// Gets a time-limited download URL for a file attached to a Receive.
    /// </summary>
    /// <param name="receiveId">Identifies the receive.</param>
    /// <param name="fileId">Identifies the file within the receive.</param>
    /// <param name="user">The principal requesting the download.</param>
    /// <returns>The download URL.</returns>
    /// <exception cref="NotFoundException">
    /// Thrown when the Receive does not exist, is not owned by the user,
    /// or does not contain the specified file.
    /// </exception>
    /// <exception cref="BadRequestException">
    /// Thrown when the user cannot be identified.
    /// </exception>
    Task<string> GetDownloadUrlAsync(Guid receiveId, string fileId, ClaimsPrincipal user);
}
