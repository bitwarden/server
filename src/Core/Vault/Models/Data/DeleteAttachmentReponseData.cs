using Bit.Core.Vault.Entities;

namespace Bit.Core.Vault.Models.Data;

public class DeleteAttachmentResponseData
{
    public Cipher Cipher { get; set; }

    public DeleteAttachmentResponseData(Cipher cipher)
    {
        Cipher = cipher;
    }
}
