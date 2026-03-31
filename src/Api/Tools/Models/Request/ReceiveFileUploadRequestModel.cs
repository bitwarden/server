using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Api.Tools.Models.Request;

/// <summary>
/// Request model for uploading a file to a Receive.
/// Sent by the anonymous uploader alongside the Receive-Secret header.
/// </summary>
public class ReceiveFileUploadRequestModel
{
    /// <summary>
    /// Encrypted file name. Encrypted with the per-file content encryption key.
    /// </summary>
    [Required]
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public required string FileName { get; set; }

    /// <summary>
    /// The per-file content encryption key, encapsulated (wrapped)
    /// with the Receive's public key.
    /// </summary>
    [Required]
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public required string EncapsulatedFileContentEncryptionKey { get; set; }
}
