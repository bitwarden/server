using Bit.Core.Models.Api;
using Bit.Core.Settings;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models.Response;

public class DeleteAttachmentResponseModel(DeleteAttachmentResponseData data, IGlobalSettings globalSettings)
    : ResponseModel("deleteAttachment")
{
    public CipherMiniResponseModel Cipher { get; set; } = new(data.Cipher, globalSettings, false);
}
