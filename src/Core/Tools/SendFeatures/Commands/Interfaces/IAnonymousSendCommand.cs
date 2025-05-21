using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;

namespace Bit.Core.Tools.SendFeatures.Commands.Interfaces;

/// <summary>
/// AnonymousSendCommand interface provides methods for managing anonymous Sends.
/// </summary>
public interface IAnonymousSendCommand
{
    /// <summary>
    /// Gets the Send file download URL for a Send object.
    /// </summary>
    /// <param name="send"><see cref="Send" /> used to help get file download url and validate file</param>
    /// <param name="fileId">FileId get file download url</param>
    /// <param name="password">A hashed and base64-encoded password. This is compared with the send's password to authorize access.</param>
    /// <returns>Async Task object with Tuple containing the string of download url and <see cref="SendAccessResult" />
    /// to determine if the user can access send.
    /// </returns>
    Task<(string, SendAccessResult)> GetSendFileDownloadUrlAsync(Send send, string fileId, string password);
}
