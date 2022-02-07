using System.Collections.Generic;
using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Response
{
    public class AttachmentResponseModel : ResponseModel
    {
        public AttachmentResponseModel(AttachmentResponseData data) : base("attachment")
        {
            Id = data.Id;
            Url = data.Url;
            FileName = data.Data.FileName;
            Key = data.Data.Key;
            Size = data.Data.Size;
            SizeName = CoreHelpers.ReadableBytesSize(data.Data.Size);
        }

        public string Id { get; set; }
        public string Url { get; set; }
        public string FileName { get; set; }
        public string Key { get; set; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
        public long Size { get; set; }
        public string SizeName { get; set; }

        public static IEnumerable<AttachmentResponseModel> FromCipher(Cipher cipher, IGlobalSettings globalSettings)
        {
            var attachments = cipher.GetAttachments();
            if (attachments == null)
            {
                return null;
            }

            return null;
            // return attachments.Select(a => new AttachmentResponseModel(a.Key, a.Value, cipher, globalSettings));
        }
    }
}
