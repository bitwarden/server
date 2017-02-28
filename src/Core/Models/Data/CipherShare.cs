using Bit.Core.Domains;

namespace Bit.Core.Models.Data
{
    public class CipherShare : Cipher
    {
        public bool ReadOnly { get; internal set; }
        public Enums.ShareStatusType? Status { get; internal set; }
    }
}
