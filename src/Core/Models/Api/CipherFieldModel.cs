using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Api
{
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
    }
}
