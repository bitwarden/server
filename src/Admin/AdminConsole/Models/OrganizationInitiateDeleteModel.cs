// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Admin.AdminConsole.Models;

public class OrganizationInitiateDeleteModel
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    [Display(Name = "Admin Email")]
    public string AdminEmail { get; set; }
}
