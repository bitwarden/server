using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class UpdateTdeOffboardingPasswordRequestModel
{
    [Required]
    [StringLength(300)]
    public string NewMasterPasswordHash { get; set; }

    [Required]
    public string Key { get; set; }

    [StringLength(50)]
    public string MasterPasswordHint { get; set; }
}
