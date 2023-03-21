using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Auth.Models.Api.Request.Accounts;

public class PreloginRequestModel
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; }
}
