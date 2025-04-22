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
    /// <param name="stream"></param>
    /// <param name="send"></param>
    /// <param name="fileId"></param>
    /// <returns></returns>
    Task UploadNewFileAsync(Stream stream, Send send, string fileId);
    /// <summary>
    /// Deletes a file from the storage.
    /// </summary>
    /// <param name="send"></param>
    /// <param name="fileId"></param>
    /// <returns></returns>
    Task DeleteFileAsync(Send send, string fileId);
    /// <summary>
    /// Deletes all files for a specific organization.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <returns></returns>
    Task DeleteFilesForOrganizationAsync(Guid organizationId);
    /// <summary>
    /// Deletes all files for a specific user.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    Task DeleteFilesForUserAsync(Guid userId);
    /// <summary>
    /// Gets the download URL for a file.
    /// </summary>
    /// <param name="send"></param>
    /// <param name="fileId"></param>
    /// <returns></returns>
    Task<string> GetSendFileDownloadUrlAsync(Send send, string fileId);
    /// <summary>
    /// Gets the upload URL for a file.
    /// </summary>
    /// <param name="send"></param>
    /// <param name="fileId"></param>
    /// <returns></returns>
    Task<string> GetSendFileUploadUrlAsync(Send send, string fileId);
    /// <summary>
    /// Validates the file size of a file in the storage.
    /// </summary>
    /// <param name="send"></param>
    /// <param name="fileId"></param>
    /// <param name="expectedFileSize"></param>
    /// <param name="leeway"></param>
    /// <returns></returns>
    Task<(bool, long?)> ValidateFileAsync(Send send, string fileId, long expectedFileSize, long leeway);
}
