using Bit.Core.Models.Api;

namespace Bit.Api.Vault.Models.Response;

/// <summary>
/// The mutated cipher returned after deleting one of its attachments. Whether it carries full or reduced
/// data is the controller's decision under PAM credential leasing; this model only wraps the result.
/// </summary>
public class DeleteAttachmentResponseModel(CipherMiniResponseModel cipher)
    : ResponseModel("deleteAttachment")
{
    public CipherMiniResponseModel Cipher { get; set; } = cipher;
}
