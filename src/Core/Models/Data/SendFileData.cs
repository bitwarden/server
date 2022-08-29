using System.Text.Json.Serialization;

namespace Bit.Core.Models.Data;

public class SendFileData : SendData
{
    public SendFileData() { }

    public SendFileData(string name, string notes, string fileName)
        : base(name, notes)
    {
        FileName = fileName;
    }

    // We serialize Size as a string since JSON (or Javascript) doesn't support full precision for long numbers
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public long Size { get; set; }

    public string Id { get; set; }
    public string FileName { get; set; }
    public bool Validated { get; set; } = true;
}
