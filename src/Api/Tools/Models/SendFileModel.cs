// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json.Serialization;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.Tools.Models;

public class SendFileModel
{
    public SendFileModel() { }

    public SendFileModel(SendFileData data)
    {
        Id = data.Id;
        FileName = data.FileName;
        Size = data.Size;
        SizeName = CoreHelpers.ReadableBytesSize(data.Size);
    }

    public string Id { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string FileName { get; set; }
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long? Size { get; set; }
    public string SizeName { get; set; }
}
