using Bit.Core.Enums;
using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Services;

/// <summary>
/// Receive File Storage Service is responsible for uploading, deleting, and validating files.
/// </summary>
public interface IReceiveFileStorageService
{
    FileUploadType FileUploadType { get; }
    /// <summary>
    ///  Uploads a new file to the storage.
    /// </summary>
    /// <param name="stream"><see cref="Stream" /> of the file</param>
    /// <param name="receive"><see cref="Receive" /> for the file</param>
    /// <param name="fileId">File id</param>
    /// <returns>Task completes once <see cref="Stream" /> and <see cref="Receive" /> have been saved to the database</returns>
    Task UploadNewFileAsync(Stream stream, Receive receive, string fileId);
    /// <summary>
    /// Deletes a file from the storage.
    /// </summary>
    /// <param name="receive"><see cref="Receive" /> used to delete file</param>
    /// <param name="fileId">File id of file to be deleted</param>
    /// <returns>Task completes once <see cref="Receive" /> has been deleted to the database</returns>
    Task DeleteFileAsync(Receive receive, string fileId);
    /// <summary>
    /// Gets the download URL for a file.
    /// </summary>
    /// <param name="receive"><see cref="Receive" /> used to help get download url for file</param>
    /// <param name="fileId">File id to help get download url for file</param>
    /// <returns>Download url as a string</returns>
    Task<string> GetReceiveFileDownloadUrlAsync(Receive receive, string fileId);
    /// <summary>
    /// Gets the upload URL for a file.
    /// </summary>
    /// <param name="receive"><see cref="Receive" /> used to help get upload url for file </param>
    /// <param name="fileId">File id to help get upload url for file</param>
    /// <returns>File upload url as string</returns>
    Task<string> GetReceiveFileUploadUrlAsync(Receive receive, string fileId);
    /// <summary>
    /// Validates the file size of a file in the storage.
    /// </summary>
    /// <param name="receive"><see cref="Receive" /> used to help validate file</param>
    /// <param name="fileId">File id to identify which file to validate</param>
    /// <param name="minimum">The minimum allowed length of the stored file in bytes.</param>
    /// <param name="maximum">The maximuim allowed length of the stored file in bytes</param>
    /// <returns>
    /// A task that completes when validation is finished. The first element of the tuple is
    /// <see langword="true" /> when validation succeeded, and false otherwise. The second element
    /// of the tuple contains the observed file length in bytes. If an error occurs during validation,
    /// this returns `-1`.
    /// </returns>
    Task<(bool valid, long length)> ValidateFileAsync(Receive receive, string fileId, long minimum, long maximum);
}
