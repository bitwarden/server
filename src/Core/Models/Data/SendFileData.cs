using System;
using Bit.Core.Models.Api;
using Newtonsoft.Json;

namespace Bit.Core.Models.Data
{
    public class SendFileData : SendData
    {
        private long _size;

        public SendFileData() { }

        public SendFileData(SendRequestModel send, string fileName)
            : base(send)
        {
            FileName = fileName;
        }

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

        public string Id { get; set; }
        public string FileName { get; set; }
        public bool Validated { get; set; } = true;
    }
}
