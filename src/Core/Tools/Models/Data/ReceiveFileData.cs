using System.Text.Json.Serialization;
using static System.Text.Json.Serialization.JsonNumberHandling;

namespace Bit.Core.Tools.Models.Data;

/// <summary>
/// Metadata for a single file uploaded to a Receive.
/// </summary>
public class ReceiveFileData
{
    /// <summary>
    /// Instantiates a <see cref="ReceiveFileData"/>.
    /// </summary>
    public ReceiveFileData() { }

    /// <inheritdoc cref="ReceiveFileData()"/>
    /// <param name="fileName">Attached file name.</param>
    public ReceiveFileData(string fileName)
    {
        FileName = fileName;
    }

    /// <summary>
    /// Uniquely identifies an uploaded file.
    /// </summary>
    /// <value>
    /// Should contain <see langword="null" /> only when a file
    /// upload is pending. Should never contain null once the
    /// file upload completes.
    /// </value>
    public string? Id { get; set; }

    /// <summary>
    /// Size of the attached file in bytes.
    /// </summary>
    /// <remarks>
    /// Serialized as a string since JSON (or Javascript) doesn't support
    /// full precision for long numbers.
    /// </remarks>
    [JsonNumberHandling(WriteAsString | AllowReadingFromString)]
    public long Size { get; set; }

    /// <summary>
    /// Attached file name.
    /// </summary>
    /// <value>
    /// Should contain a non-empty string once the file upload completes.
    /// </value>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// When true the uploaded file's length was confirmed within
    /// the expected tolerance and below the maximum supported
    /// file size.
    /// </summary>
    public bool Validated { get; set; } = false;
}
