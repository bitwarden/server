namespace Bit.Api.Models.Request;

public class AttachmentRequestModel
{
    public string Key { get; set; }
    public string FileName { get; set; }
    public long FileSize { get; set; }
    public bool AdminRequest { get; set; } = false;
}
