#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using static System.Text.Json.Serialization.JsonNumberHandling;

namespace Bit.Core.Tools.Models.Data;

/// <summary>
/// A file secret being sent.
/// </summary>
public class SendFileData : SendData
{
    /// <summary>
    /// Instantiates a <see cref="SendFileData"/>.
    /// </summary>
    public SendFileData() { }

    /// <inheritdoc cref="SendFileData()"/>
    /// <param name="name">Attached file name.</param>
    /// <param name="notes">User-provided private notes of the send.</param>
    /// <param name="fileName">Attached file name.</param>
    public SendFileData(string name, string? notes, string fileName)
        : base(name, notes)
    {
        FileName = fileName;
    }

    /// <summary>
    /// Size of the attached file in bytes.
    /// </summary>
    /// <remarks>
    /// Serialized as a string since JSON (or Javascript)  doesn't support
    /// full precision for long numbers
    /// </remarks>
    [JsonNumberHandling(WriteAsString | AllowReadingFromString)]
    public long Size { get; set; }

    /// <summary>
    /// Uniquely identifies an uploaded file.
    /// </summary>
    /// <value>
    /// Should contain <see langword="null" /> only when a file
    /// upload is pending. Should never contain null once the
    /// file upload completes.
    /// </value>
    [DisallowNull]
    public string? Id { get; set; }

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
    public bool Validated { get; set; } = true;
}
