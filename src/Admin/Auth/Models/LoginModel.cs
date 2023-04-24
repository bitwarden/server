using System.ComponentModel.DataAnnotations;

namespace Bit.Admin.Auth.Models;

public class LoginModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
    public string ReturnUrl { get; set; }
    public string Error { get; set; }
    public string Success { get; set; }
}
