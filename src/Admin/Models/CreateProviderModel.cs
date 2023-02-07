using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities.Provider;
using Bit.Core.Enums.Provider;
using Bit.SharedWeb.Utilities;

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
                    var ownerEmailDisplayName = nameof(OwnerEmail).GetDisplayAttribute<CreateProviderModel>()?.GetName();
                    yield return new ValidationResult($"The {ownerEmailDisplayName} field is required.");
                }
                break;
            case ProviderType.Reseller:
                if (string.IsNullOrWhiteSpace(BusinessName))
                {
                    var businessNameDisplayName = nameof(BusinessName).GetDisplayAttribute<CreateProviderModel>()?.GetName();
                    yield return new ValidationResult($"The {businessNameDisplayName} field is required.");
                }
                if (string.IsNullOrWhiteSpace(BillingEmail))
                {
                    var billingEmailDisplayName = nameof(BillingEmail).GetDisplayAttribute<CreateProviderModel>()?.GetName();
                    yield return new ValidationResult($"The {billingEmailDisplayName} field is required.");
                }
                break;
        }
    }
}
