using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.ReceiveFeatures.Commands.Interfaces;

public interface IUploadReceiveFileCommand
{
    /// <summary>
    /// Generates a file ID, persists it in the Receive's Data field, and returns a
    /// time-limited upload URL for the given Receive.
    /// </summary>
    /// <param name="receive">The Receive entity. Its Data field will be updated with the new fileId.</param>
    /// <param name="fileName">The encrypted file name provided by the uploader.</param>
    /// <param name="fileLength">The expected file size in bytes (used for post-upload validation).</param>
    /// <param name="encapsulatedFileEncryptionKey">The file encryption key encapsulated with the Receive's public key.</param>
    Task<(string url, string fileId)> GetUploadUrlAsync(Receive receive, string fileName, long fileLength, string encapsulatedFileEncryptionKey);

    /// <summary>
    /// Validates that a file was successfully uploaded to storage, checks its size, and
    /// marks the file as validated in the Receive entity.
    /// </summary>
    /// <returns>True if validation succeeded; false if the file was invalid and cleaned up.</returns>
    Task<bool> ValidateFileAsync(Receive receive);
}
