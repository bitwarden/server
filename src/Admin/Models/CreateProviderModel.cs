using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities.Provider;
using Bit.Core.Enums.Provider;

namespace Bit.Admin.Models;

public class CreateProviderModel
{
    public CreateProviderModel() { }

    [Display(Name = "Provider Type")]
    public ProviderType Type { get; set; }

    [Display(Name = "Owner Email")]
    [Required]
    public string OwnerEmail { get; set; }

    [Display(Name = "Business Name")]
    public string BusinessName { get; set; }

    [Display(Name = "Primary Billing Email")]
    public string BillingEmail { get; set; }

    public virtual Provider ToProvider()
    {
        return new Provider()
        {
            Type = Type,
            BusinessName = BusinessName,
            BillingEmail = BillingEmail?.ToLowerInvariant().Trim()
        };
    }
}
