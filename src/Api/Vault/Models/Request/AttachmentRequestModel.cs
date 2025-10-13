// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Api.Vault.Models.Request;

public class AttachmentRequestModel
{
    public string Key { get; set; }
    public string FileName { get; set; }
    public long FileSize { get; set; }
    public bool AdminRequest { get; set; } = false;

    /// The last known revision date of the Cipher that this attachment belongs to.
    public DateTime? LastKnownRevisionDate { get; set; } = null;
}
