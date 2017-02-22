using Bit.Core.Domains;

namespace Bit.Core.Models.Data
{
    public class CipherShare : Cipher
    {
        public string Key { get; internal set; }
        public string Permissions { get; internal set; }
        public Enums.ShareStatusType? Status { get; internal set; }
    }
}
