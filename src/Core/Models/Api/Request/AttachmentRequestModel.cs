namespace Bit.Core.Models.Api
{
    public class AttachmentRequestModel
    {
        public string key { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public bool AdminRequest { get; set; } = false;
    }
}
