using System.Security.Claims;
using Bit.Core.Exceptions;

namespace Bit.Core.Tools.ReceiveFeatures.Queries.Interfaces;

/// <summary>
/// Gets a download URL for a Receive's file, verifying ownership.
/// </summary>
public interface IGetReceiveFileDownloadQuery
{
    /// <summary>
    /// Gets a time-limited download URL for the file attached to a Receive.
    /// </summary>
    /// <param name="receiveId">Identifies the receive.</param>
    /// <param name="user">The principal requesting the download.</param>
    /// <returns>A tuple of the download URL and the file ID.</returns>
    /// <exception cref="NotFoundException">
    /// Thrown when the Receive does not exist or is not owned by the user.
    /// </exception>
    /// <exception cref="BadRequestException">
    /// Thrown when the Receive has no uploaded file or the user cannot be identified.
    /// </exception>
    Task<(string Url, string FileId)> GetDownloadUrlAsync(Guid receiveId, ClaimsPrincipal user);
}
