// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Identity.Models.Request.Accounts;

public class PasswordPreloginRequestModel
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; }
}
