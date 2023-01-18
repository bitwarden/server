using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities.Provider;
using Bit.Core.Enums.Provider;

namespace Bit.Admin.Models;

public class CreateProviderModel : IValidatableObject
{
    public CreateProviderModel() { }

    [Display(Name = "Provider Type")]
    public ProviderType Type { get; set; }

    [Display(Name = "Owner Email")]
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

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        switch (Type)
        {
            case ProviderType.Msp:
                if (string.IsNullOrWhiteSpace(OwnerEmail))
                {
                    yield return new ValidationResult("The Owner Email field is required.");
                }
                break;
            case ProviderType.Reseller:
                if (string.IsNullOrWhiteSpace(BusinessName))
                {
                    yield return new ValidationResult("The Business Name field is required.");
                }
                if (string.IsNullOrWhiteSpace(BillingEmail))
                {
                    yield return new ValidationResult("The Primary Billing Email field is required.");
                }
                break;
        }
    }
}
