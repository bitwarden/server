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
    /// <param name="encapsulatedFileContentEncryptionKey">
    /// The per-file content encryption key, encapsulated with the Receive's public key.
    /// </param>
    Task<(string Url, string FileId)> GetUploadUrlAsync(
        Receive receive, string fileName, string encapsulatedFileContentEncryptionKey);
}
