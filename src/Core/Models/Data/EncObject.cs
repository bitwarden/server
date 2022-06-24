using Bit.Core.Enums;

namespace Bit.Core.Models.Data
{
    public class EncObject
    {
        public EncryptionType Type { get; set; }
        public string Data { get; set; }
        public string Iv { get; set; }
        public string Mac { get; set; }
    }
}
