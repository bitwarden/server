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
    /// <param name="send">Send object to help get file download url and validate file</param>
    /// <param name="fileId">FileId get file download url</param>
    /// <param name="password">Password will be validated and used to determine access</param>
    /// <returns>Async Task object with Tuple containing the string of download url, boolean that identifies if
    /// passwordRequiredError occurred, and another boolean that identifies if passwordInvalidError occurred.
    /// </returns>
    Task<(string, bool, bool)> GetSendFileDownloadUrlAsync(Send send, string fileId, string password);
}
