using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Billing.Models.Requests;

public class KeysRequestBody
{
    [Required(ErrorMessage = "'publicKey' must be provided")]
    public string PublicKey { get; set; }
    [Required(ErrorMessage = "'encryptedPrivateKey' must be provided")]
    public string EncryptedPrivateKey { get; set; }
}
