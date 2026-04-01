using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Api.Tools.Models.Request;

/// <summary>
/// Request model for anonymous file upload to a Receive.
/// </summary>
public class ReceiveFileUploadRequestModel
{
    /// <summary>
    /// The encrypted file name.
    /// </summary>
    [Required]
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public required string FileName { get; set; }

    /// <summary>
    /// Expected file size in bytes.
    /// </summary>
    [Required]
    [Range(1, long.MaxValue)]
    public long FileLength { get; set; }

    /// <summary>
    /// The file encryption key encapsulated with the Receive's public key.
    /// </summary>
    [Required]
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public required string EncapsulatedFileEncryptionKey { get; set; }
}
