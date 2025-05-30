using Bit.Core.Enums;
using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Services;

/// <summary>
/// Send File Storage Service is responsible for uploading, deleting, and validating files
/// whether they are in local storage or in cloud storage.
/// </summary>
public interface ISendFileStorageService
{
    FileUploadType FileUploadType { get; }
    /// <summary>
    ///  Uploads a new file to the storage.
    /// </summary>
    /// <param name="stream"><see cref="Stream" /> of the file</param>
    /// <param name="send"><see cref="Send" /> for the file</param>
    /// <param name="fileId">File id</param>
    /// <returns>Task completes once <see cref="Stream" /> and <see cref="Send" /> have been saved to the database</returns>
    Task UploadNewFileAsync(Stream stream, Send send, string fileId);
    /// <summary>
    /// Deletes a file from the storage.
    /// </summary>
    /// <param name="send"><see cref="Send" /> used to delete file</param>
    /// <param name="fileId">File id of file to be deleted</param>
    /// <returns>Task completes once <see cref="Send" /> has been deleted to the database</returns>
    Task DeleteFileAsync(Send send, string fileId);
    /// <summary>
    /// Deletes all files for a specific organization.
    /// </summary>
    /// <param name="organizationId"><see cref="Guid" />  used to delete all files pertaining to organization</param>
    /// <returns>Task completes after running code to delete files by organization id</returns>
    Task DeleteFilesForOrganizationAsync(Guid organizationId);
    /// <summary>
    /// Deletes all files for a specific user.
    /// </summary>
    /// <param name="userId"><see cref="Guid" /> used to delete all files pertaining to user</param>
    /// <returns>Task completes after running code to delete files by user id</returns>
    Task DeleteFilesForUserAsync(Guid userId);
    /// <summary>
    /// Gets the download URL for a file.
    /// </summary>
    /// <param name="send"><see cref="Send" /> used to help get download url for file</param>
    /// <param name="fileId">File id to help get download url for file</param>
    /// <returns>Download url as a string</returns>
    Task<string> GetSendFileDownloadUrlAsync(Send send, string fileId);
    /// <summary>
    /// Gets the upload URL for a file.
    /// </summary>
    /// <param name="send"><see cref="Send" /> used to help get upload url for file </param>
    /// <param name="fileId">File id to help get upload url for file</param>
    /// <returns>File upload url as string</returns>
    Task<string> GetSendFileUploadUrlAsync(Send send, string fileId);
    /// <summary>
    /// Validates the file size of a file in the storage.
    /// </summary>
    /// <param name="send"><see cref="Send" /> used to help validate file</param>
    /// <param name="fileId">File id to identify which file to validate</param>
    /// <param name="expectedFileSize">Expected file size of the file</param>
    /// <param name="leeway">
    /// Send file size tolerance in bytes. When an uploaded file's `expectedFileSize`
    /// is outside of the leeway, the storage operation fails.
    /// </param>
    /// <throws>
    /// ❌ Fill this in with an explanation of the error thrown when `leeway` is incorrect
    /// </throws>
    /// <returns>Task object for async operations with Tuple of boolean that determines if file was valid and long that
    /// the actual file size of the file.
    /// </returns>
    Task<(bool, long?)> ValidateFileAsync(Send send, string fileId, long expectedFileSize, long leeway);
}
