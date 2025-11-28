using System.ComponentModel.DataAnnotations;

namespace Bit.Admin.Models;

public class PromoteProviderServiceUserModel
{
    [Required]
    [Display(Name = "Provider Service User Id")]
    public Guid? UserId { get; set; }
    [Required]
    [Display(Name = "Provider Id")]
    public Guid? ProviderId { get; set; }
}
