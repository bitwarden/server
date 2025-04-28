using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.SendFeatures.Commands.Interfaces;

/// <summary>
/// AnonymousSendCommand interface provides methods for managing anonymous Sends.
/// </summary>
public interface IAnonymousSendCommand
{
    /// <summary>
    /// Gets the Send file download URL for a Send object.
    /// </summary>
    /// <param name="send"><see cref="Send" /> to help get file download url and validate file</param>
    /// <param name="fileId">File id get file download url</param>
    /// <param name="password">Password will be validated and used to determine access</param>
    /// <returns>Async Task object with Tuple containing the string of download url, boolean that is true when a
    /// passwordRequiredError occurred, and another boolean that is true when a passwordInvalidError occurred.
    /// </returns>
    Task<(string, bool, bool)> GetSendFileDownloadUrlAsync(Send send, string fileId, string password);
}
