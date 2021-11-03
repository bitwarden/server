using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Core.Models.Data
{
    public class CipherFieldData
    {
        public CipherFieldData() { }

        public CipherFieldData(CipherFieldModel field)
        {
            Type = field.Type;
            Name = field.Name;
            Value = field.Value;
            LinkedId = field.LinkedId ?? null;
        }

        public FieldType Type { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public int? LinkedId { get; set; }
    }
}
