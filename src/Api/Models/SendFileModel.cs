using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.Models
{
    public class SendFileModel
    {
        public SendFileModel() { }

        public SendFileModel(SendFileData data)
        {
            Id = data.Id;
            FileName = data.FileName;
            Size = data.SizeString;
            SizeName = CoreHelpers.ReadableBytesSize(data.Size);
        }

        public string Id { get; set; }
        [EncryptedString]
        [EncryptedStringLength(1000)]
        public string FileName { get; set; }
        public string Size { get; set; }
        public string SizeName { get; set; }
    }
}
