using System.ComponentModel.DataAnnotations;

namespace Bit.Billing.Models;

public class LoginModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
}
