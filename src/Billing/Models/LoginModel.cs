// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Billing.Models;

public class LoginModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
}
