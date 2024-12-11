using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Api.Vault.Models.Response;

public class AttachmentUploadDataResponseModel : ResponseModel
{
    public string AttachmentId { get; set; }
    public string Url { get; set; }
    public FileUploadType FileUploadType { get; set; }
    public CipherResponseModel CipherResponse { get; set; }
    public CipherMiniResponseModel CipherMiniResponse { get; set; }

    public AttachmentUploadDataResponseModel()
        : base("attachment-fileUpload") { }
}
