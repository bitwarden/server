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
    /// <param name="send">Send object that is to be saved to database</param>
    /// <returns>Task object for async operations</returns>
    Task SaveSendAsync(Send send);

    /// <summary>
    /// Save File to the database.
    /// </summary>
    /// <param name="send">Send object that is to be saved to database</param>
    /// <param name="data">SendFileData to be saved to database</param>
    /// <param name="fileLength">Length of file help with saving to database</param>
    /// <returns>Task object for async operations with file upload url</returns>
    Task<string> SaveFileSendAsync(Send send, SendFileData data, long fileLength);
    /// <summary>
    /// Upload a file to an existing Send.
    /// </summary>
    /// <param name="stream">Stream of file to be uploaded</param>
    /// <param name="send">Send object to help with uploading file</param>
    /// <returns>Task object for async operations</returns>
    Task UploadFileToExistingSendAsync(Stream stream, Send send);

    /// <summary>
    /// Deletes a Send from the database.
    /// </summary>
    /// <param name="send">Send object to be used to delete from database</param>
    /// <returns>Task object for async operations</returns>
    Task DeleteSendAsync(Send send);
}
