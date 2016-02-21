using Bit.Core.Enums;

namespace Bit.Core.Domains
{
    public class Site : Cipher, IDataObject
    {
        public string FolderId { get; set; }
        public string Uri { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Notes { get; set; }
    }
}
