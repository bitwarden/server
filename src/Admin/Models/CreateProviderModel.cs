using System.ComponentModel.DataAnnotations;

namespace Bit.Admin.Models;

public class CreateProviderModel
{
    public CreateProviderModel() { }

    [Display(Name = "Owner Email")]
    [Required]
    public string OwnerEmail { get; set; }
}
