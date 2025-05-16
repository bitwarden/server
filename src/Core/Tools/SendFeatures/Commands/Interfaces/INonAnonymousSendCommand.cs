using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;

namespace Bit.Core.Tools.SendFeatures.Commands.Interfaces;

/// <summary>
/// NonAnonymousSendCommand interface provides methods for managing non-anonymous Sends.
/// </summary>
public interface INonAnonymousSendCommand
{
    /// <summary>
    /// Saves a <see cref="Send" /> to the database.
    /// </summary>
    /// <param name="send"><see cref="Send" /> that will save to database</param>
    /// <returns>Task completes as <see cref="Send" /> saves to the database</returns>
    Task SaveSendAsync(Send send);

    /// <summary>
    /// Saves the <see cref="Send" /> and <see cref="SendFileData" /> to the database.
    /// </summary>
    /// <param name="send"><see cref="Send" /> that will save to the database</param>
    /// <param name="data"><see cref="SendFileData" /> that will save to file storage</param>
    /// <param name="fileLength">Length of file help with saving to file storage</param>
    /// <returns>Task object for async operations with file upload url</returns>
    Task<string> SaveFileSendAsync(Send send, SendFileData data, long fileLength);

    /// <summary>
    /// Upload a file to an existing <see cref="Send" />.
    /// </summary>
    /// <param name="stream"><see cref="Stream" /> of file to be uploaded. The <see cref="Stream" /> position
    /// will be set to 0 before uploading the file.</param>
    /// <param name="send"><see cref="Send" /> used to help with uploading file</param>
    /// <returns>Task completes after saving <see cref="Stream" /> and <see cref="Send" /> metadata to the file storage</returns>
    Task UploadFileToExistingSendAsync(Stream stream, Send send);

    /// <summary>
    /// Deletes a <see cref="Send" /> from the database and file storage.
    /// </summary>
    /// <param name="send"><see cref="Send" /> is used to delete from database and file storage</param>
    /// <returns>Task completes once <see cref="Send" /> has been deleted from database and file storage.</returns>
    Task DeleteSendAsync(Send send);

    /// <summary>
    /// Stores the confirmed file size of a send; when the file size cannot be confirmed, the send is deleted.
    /// </summary>
    /// <param name="send">The <see cref="Send" /> this command acts upon</param>
    /// <returns><see langword="true" /> when the file is confirmed, otherwise <see langword="false" /></returns>
    /// <remarks>
    /// When a file size cannot be confirmed, we assume we're working with a rogue client. The send is deleted out of
    ///  an abundance of caution.
    /// </remarks>
    Task<bool> ConfirmFileSize(Send send);
}
