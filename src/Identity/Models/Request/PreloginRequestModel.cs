using System.ComponentModel.DataAnnotations;

namespace Bit.Identity.Models.Request;

public class PreloginRequestModel
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; }
}
