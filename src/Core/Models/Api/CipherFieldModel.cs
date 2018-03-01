using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

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
        }

        public FieldType Type { get; set; }
        [StringLength(1000)]
        public string Name { get; set; }
        [StringLength(1000)]
        public string Value { get; set; }
    }
}
