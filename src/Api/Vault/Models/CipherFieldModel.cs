using Bit.Core.Utilities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models;

public class CipherFieldModel
{
    public CipherFieldModel() { }

    public CipherFieldModel(CipherFieldData data)
    {
        Type = data.Type;
        Name = data.Name;
        Value = data.Value;
        LinkedId = data.LinkedId ?? null;
    }

    public FieldType Type { get; set; }

    [EncryptedStringLength(1000)]
    public string Name { get; set; }

    [EncryptedStringLength(5000)]
    public string Value { get; set; }
    public int? LinkedId { get; set; }

    public CipherFieldData ToCipherFieldData()
    {
        return new CipherFieldData
        {
            Type = Type,
            Name = Name,
            Value = Value,
            LinkedId = LinkedId ?? null,
        };
    }
}
