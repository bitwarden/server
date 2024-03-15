using System.ComponentModel.DataAnnotations;

namespace Bit.Admin.AdminConsole.Models;

public class OrganizationInitiateDeleteModel
{
    [Required]
    [Display(Name = "Admin Email")]
    public string AdminEmail { get; set; }
}
