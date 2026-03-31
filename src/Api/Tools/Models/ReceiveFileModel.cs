using System.Text.Json.Serialization;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.Tools.Models;

public class ReceiveFileModel
{
    public ReceiveFileModel() { }

    public ReceiveFileModel(ReceiveFileData data)
    {
        Id = data.Id;
        FileName = data.FileName;
        Size = data.Size;
        SizeName = CoreHelpers.ReadableBytesSize(data.Size);
    }

    public string? Id { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string FileName { get; set; } = string.Empty;

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long? Size { get; set; }

    public string? SizeName { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string EncapsulatedFileContentEncryptionKey { get; set; } = string.Empty;
}
