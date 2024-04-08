using System.ComponentModel.DataAnnotations;

namespace Bit.Admin.AdminConsole.Models;

public class ProviderInitiateDeleteModel
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string AdminEmail { get; set; }
}
