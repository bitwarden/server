using System;
using Newtonsoft.Json;

namespace Bit.Core.Models.Data
{
    public class SendFileData : SendData
    {
        private long _size;

        public SendFileData() { }

        public SendFileData(string name, string notes, string fileName)
            : base(name, notes)
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
