using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;

namespace Bit.Core.Tools.SendFeatures.Commands.Interfaces;

/// <summary>
/// NonAnonymousSendCommand interface provides methods for managing non-anonymous Sends.
/// </summary>
public interface INonAnonymousSendCommand
{
    /// <summary>
    /// Saves a Send to the database.
    /// </summary>
    /// <param name="send"></param>
    /// <returns></returns>
    Task SaveSendAsync(Send send);

    /// <summary>
    /// Save File to the database.
    /// </summary>
    /// <param name="send"></param>
    /// <param name="data"></param>
    /// <param name="fileLength"></param>
    /// <returns></returns>
    Task<string> SaveFileSendAsync(Send send, SendFileData data, long fileLength);
    /// <summary>
    /// Upload a file to an existing Send.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="send"></param>
    /// <returns></returns>
    Task UploadFileToExistingSendAsync(Stream stream, Send send);

    /// <summary>
    /// Deletes a Send from the database.
    /// </summary>
    /// <param name="send"></param>
    /// <returns></returns>
    Task DeleteSendAsync(Send send);
}
