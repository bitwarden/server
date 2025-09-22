﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.Api;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models.Response;

public class AttachmentResponseModel : ResponseModel
{
    public AttachmentResponseModel(AttachmentResponseData data) : base("attachment")
    {
        Id = data.Id;
        Url = data.Url;
        FileName = data.Data.FileName;
        Key = data.Data.Key;
        Size = data.Data.Size.ToString();
        SizeName = CoreHelpers.ReadableBytesSize(data.Data.Size);
    }

    public AttachmentResponseModel(string id, CipherAttachment.MetaData data, Cipher cipher,
        IGlobalSettings globalSettings)
        : base("attachment")
    {
        Id = id;
        Url = $"{globalSettings.Attachment.BaseUrl}/{cipher.Id}/{id}";
        FileName = data.FileName;
        Key = data.Key;
        Size = data.Size.ToString();
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
