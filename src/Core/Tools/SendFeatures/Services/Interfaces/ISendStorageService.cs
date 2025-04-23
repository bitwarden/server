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
    /// <param name="stream">Stream of the file</param>
    /// <param name="send">Send object for the file</param>
    /// <param name="fileId">File id</param>
    /// <returns>Task object for async operations</returns>
    Task UploadNewFileAsync(Stream stream, Send send, string fileId);
    /// <summary>
    /// Deletes a file from the storage.
    /// </summary>
    /// <param name="send">Send object for file to be deleted</param>
    /// <param name="fileId">File id of file to be deleted</param>
    /// <returns>Task object for async operations</returns>
    Task DeleteFileAsync(Send send, string fileId);
    /// <summary>
    /// Deletes all files for a specific organization.
    /// </summary>
    /// <param name="organizationId">OrganizationId to delete all files pertaining to organization</param>
    /// <returns>Task object for async operations</returns>
    Task DeleteFilesForOrganizationAsync(Guid organizationId);
    /// <summary>
    /// Deletes all files for a specific user.
    /// </summary>
    /// <param name="userId">UserId to delete all files pertaining to user</param>
    /// <returns>Task object for async operations</returns>
    Task DeleteFilesForUserAsync(Guid userId);
    /// <summary>
    /// Gets the download URL for a file.
    /// </summary>
    /// <param name="send">Send object help get download url for file</param>
    /// <param name="fileId">File id to help get download url for file</param>
    /// <returns>Task object for async operations with download url as a string</returns>
    Task<string> GetSendFileDownloadUrlAsync(Send send, string fileId);
    /// <summary>
    /// Gets the upload URL for a file.
    /// </summary>
    /// <param name="send">Send object help get upload url for file </param>
    /// <param name="fileId">File id to help get upload url for file</param>
    /// <returns>Task object for async operations with file upload url as string</returns>
    Task<string> GetSendFileUploadUrlAsync(Send send, string fileId);
    /// <summary>
    /// Validates the file size of a file in the storage.
    /// </summary>
    /// <param name="send">Send object help validate file</param>
    /// <param name="fileId">File id to identify which file to validate</param>
    /// <param name="expectedFileSize">Expected file size of the file</param>
    /// <param name="leeway">Leeway will be used as tolerance range when validating the file size</param>
    /// <returns>Task object for async operations with Tuple of boolean that determines if file was valid and long that
    /// the actual file size of the file.
    /// </returns>
    Task<(bool, long?)> ValidateFileAsync(Send send, string fileId, long expectedFileSize, long leeway);
}
