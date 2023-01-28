using Bit.Core.Enums;

namespace Bit.Core.Models.Data;

public class CipherFieldData
{
    public CipherFieldData() { }

    public FieldType Type { get; set; }
    public string Name { get; set; }
    public string Value { get; set; }
    public int? LinkedId { get; set; }
}
