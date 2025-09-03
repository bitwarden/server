// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Vault.Enums;

namespace Bit.Core.Vault.Models.Data;

public class CipherFieldData
{
    public CipherFieldData() { }

    public FieldType Type { get; set; }
    public string Name { get; set; }
    public string Value { get; set; }
    public int? LinkedId { get; set; }
}
