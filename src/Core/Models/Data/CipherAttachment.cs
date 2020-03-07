using Newtonsoft.Json;
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
            private long _size;

            [JsonIgnore]
            public long Size
            {
                get { return _size; }
                set { _size = value; }
            }

            // We serialize Size as a string since JSON (or Javascript) doesn't support full precision for long numbers
            [JsonProperty("Size")]
            public string SizeString
            {
                get { return _size.ToString(); }
                set { _size = Convert.ToInt64(value); }
            }

            public string FileName { get; set; }
            public string Key { get; set; }
        }
    }
}
