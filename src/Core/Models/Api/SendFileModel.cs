using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Api
{
    public class SendFileModel
    {
        public SendFileModel() { }

        public SendFileModel(SendFileData data, GlobalSettings globalSettings)
        {
            Id = data.Id;
            Url = $"{globalSettings.Send.BaseUrl}/{data.Id}";
            FileName = data.FileName;
            Size = data.SizeString;
            SizeName = CoreHelpers.ReadableBytesSize(data.Size);
        }

        public string Id { get; set; }
        public string Url { get; set; }
        [EncryptedString]
        [EncryptedStringLength(1000)]
        public string FileName { get; set; }
        public string Size { get; set; }
        public string SizeName { get; set; }
    }
}
