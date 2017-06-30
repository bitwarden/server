using System;

namespace Bit.Core.Models.Data
{
    public class CipherAttachment
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public string AttachmentId { get; set; }
        public string AttachmentData { get; set; }

        public class MetaData
        {
            public long Size { get; set; }
            public string FileName { get; set; }
        }
    }
}
