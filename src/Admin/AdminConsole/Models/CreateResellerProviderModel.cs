using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.SharedWeb.Utilities;

namespace Bit.Admin.AdminConsole.Models;

public class CreateResellerProviderModel : IValidatableObject
{
    [Display(Name = "Name")]
    public string Name { get; set; }

    [Display(Name = "Business Name")]
    public string BusinessName { get; set; }

    [Display(Name = "Primary Billing Email")]
    public string BillingEmail { get; set; }

    public virtual Provider ToProvider()
    {
        return new Provider
        {
            Name = Name,
            BusinessName = BusinessName,
            BillingEmail = BillingEmail?.ToLowerInvariant().Trim(),
            Type = ProviderType.Reseller
        };
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            var nameDisplayName = nameof(Name).GetDisplayAttribute<CreateProviderModel>()?.GetName() ?? nameof(Name);
            yield return new ValidationResult($"The {nameDisplayName} field is required.");
        }
        if (string.IsNullOrWhiteSpace(BusinessName))
        {
            var businessNameDisplayName = nameof(BusinessName).GetDisplayAttribute<CreateProviderModel>()?.GetName() ?? nameof(BusinessName);
            yield return new ValidationResult($"The {businessNameDisplayName} field is required.");
        }
        if (string.IsNullOrWhiteSpace(BillingEmail))
        {
            var billingEmailDisplayName = nameof(BillingEmail).GetDisplayAttribute<CreateProviderModel>()?.GetName() ?? nameof(BillingEmail);
            yield return new ValidationResult($"The {billingEmailDisplayName} field is required.");
        }
    }
}
