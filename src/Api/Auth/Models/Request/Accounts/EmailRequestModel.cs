using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class EmailRequestModel : SecretVerificationRequestModel
{
    [Required]
    [StrictEmailAddress]
    [StringLength(256)]
    public string NewEmail { get; set; }

    [Required]
    [StringLength(300)]
    public string NewMasterPasswordHash { get; set; }

    [Required]
    public string Token { get; set; }

    [Required]
    public string Key { get; set; }
}
