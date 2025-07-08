// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Vault.Entities;

namespace Bit.Core.Vault.Models.Data;

public class AttachmentResponseData
{
    public string Id { get; set; }
    public CipherAttachment.MetaData Data { get; set; }
    public Cipher Cipher { get; set; }
    public string Url { get; set; }
}
