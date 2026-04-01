using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.ReceiveFeatures.Commands.Interfaces;

public interface IUploadReceiveFileCommand
{
    /// <summary>
    /// Generates a file ID, records file metadata in the Receive's data,
    /// and returns a time-limited upload URL along with the generated file ID.
    /// </summary>
    /// <param name="receive">The Receive to upload to.</param>
    /// <param name="fileName">Encrypted file name.</param>
    /// <param name="fileLength">Expected file size in bytes.</param>
    /// <param name="encapsulatedFileContentEncryptionKey">
    /// The per-file content encryption key, encapsulated with the Receive's public key.
    /// </param>
    Task<(string Url, string FileId)> GetUploadUrlAsync(
        Receive receive, string fileName, long fileLength, string encapsulatedFileContentEncryptionKey);

    /// <summary>
    /// Validates that a file was successfully uploaded to storage, checks its size,
    /// and marks the file as validated in the Receive entity.
    /// </summary>
    /// <returns>True if validation succeeded; false if the file was invalid and cleaned up.</returns>
    Task<bool> ValidateFileAsync(Receive receive, string fileId);
}
