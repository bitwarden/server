using Bit.Core.Models.Table;

namespace Bit.Core.Models.Data
{
    public class AttachmentResponseData
    {
        public string Id { get; set; }
        public CipherAttachment.MetaData Data { get; set; }
        public Cipher Cipher { get; set; }
        public string Url { get; set; }
    }
}
