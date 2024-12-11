using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class RegenerateTwoFactorRequestModel
{
    [Required]
    public string MasterPasswordHash { get; set; }

    [Required]
    [StringLength(50)]
    public string Token { get; set; }
}
