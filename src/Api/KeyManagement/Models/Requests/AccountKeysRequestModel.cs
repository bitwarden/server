using System.ComponentModel.DataAnnotations;

namespace Bit.Api.KeyManagement.Models.Request.Accounts;

public class AccountKeysRequestModel
{
    [Required]
    public string UserKeyEncryptedAccountPrivateKey { get; set; }
    [Required]
    public string AccountPublicKey { get; set; }
}
