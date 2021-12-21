using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Response
{
    public class AttachmentResponseModel : ResponseModel
    {
        public AttachmentResponseModel(string id, CipherAttachment.MetaData data, Cipher cipher,
            IGlobalSettings globalSettings)
            : base("attachment")
        {
            Id = id;
            Url = $"{globalSettings.Attachment.BaseUrl}/{cipher.Id}/{id}";
            FileName = data.FileName;
            Key = data.Key;
            Size = data.SizeString;
            SizeName = CoreHelpers.ReadableBytesSize(data.Size);
        }

        public string Id { get; set; }
        public string Url { get; set; }
        public string FileName { get; set; }
        public string Key { get; set; }
        public string Size { get; set; }
        public string SizeName { get; set; }

        public static IEnumerable<AttachmentResponseModel> FromCipher(Cipher cipher, IGlobalSettings globalSettings)
        {
            var attachments = cipher.GetAttachments();
            if (attachments == null)
            {
                return null;
            }

            return attachments.Select(a => new AttachmentResponseModel(a.Key, a.Value, cipher, globalSettings));
        }
    }
}
