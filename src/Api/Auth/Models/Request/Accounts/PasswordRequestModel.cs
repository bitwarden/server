// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class PasswordRequestModel : SecretVerificationRequestModel
{
    [Required]
    [StringLength(300)]
    public string NewMasterPasswordHash { get; set; }
    [StringLength(50)]
    public string MasterPasswordHint { get; set; }
    [Required]
    public string Key { get; set; }
}
