namespace Bit.Core.Tools.Models.Data;

/// <summary>
/// Top-level data structure stored in <see cref="Entities.Receive.Data"/>.
/// Contains the encrypted Receive label and metadata for all uploaded files.
/// </summary>
public class ReceiveData
{
    /// <summary>
    /// User-provided name of the Receive. Encrypted.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Metadata for each file uploaded to this Receive.
    /// </summary>
    public List<ReceiveFileData> Files { get; set; } = new();
}
