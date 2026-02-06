// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Utilities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models;

public class CipherAttachmentModel
{
    public CipherAttachmentModel() { }

    public CipherAttachmentModel(CipherAttachment.MetaData data)
    {
        FileName = data.FileName;
        Key = data.Key;
    }

    [EncryptedStringLength(1000)]
    public string FileName { get; set; }
    [EncryptedStringLength(1000)]
    public string Key { get; set; }
}
