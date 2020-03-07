using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Linq;

namespace Bit.Core.Models.Api
{
    public class AttachmentResponseModel : ResponseModel
    {
        public AttachmentResponseModel(string id, CipherAttachment.MetaData data, Cipher cipher,
            GlobalSettings globalSettings)
            : base("attachment")
        {
            Id = id;
            Url = $"{globalSettings.Attachment.BaseUrl}/{cipher.Id}/{id}";
            FileName = data.FileName;
            Key = data.Key;
            Size = data.SizeString;
            SizeName = Utilities.CoreHelpers.ReadableBytesSize(data.Size);
        }

        public string Id { get; set; }
        public string Url { get; set; }
        public string FileName { get; set; }
        public string Key { get; set; }
        public string Size { get; set; }
        public string SizeName { get; set; }

        public static IEnumerable<AttachmentResponseModel> FromCipher(Cipher cipher, GlobalSettings globalSettings)
        {
            var attachments = cipher.GetAttachments();
            if(attachments == null)
            {
                return null;
            }

            return attachments.Select(a => new AttachmentResponseModel(a.Key, a.Value, cipher, globalSettings));
        }
    }
}
