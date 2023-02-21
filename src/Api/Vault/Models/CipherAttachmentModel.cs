using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.Models;

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
