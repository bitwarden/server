// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Admin.Billing.Models;

public class MigrateProvidersRequestModel
{
    [Required]
    [Display(Name = "Provider IDs")]
    public string ProviderIds { get; set; }
}
