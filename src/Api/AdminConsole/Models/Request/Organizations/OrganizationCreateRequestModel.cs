using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Enums;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public abstract class OrganizationCreateRequestModel : OrganizationCreateRequestBase, IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PlanType != PlanType.Free && string.IsNullOrWhiteSpace(PaymentToken))
        {
            yield return new ValidationResult("Payment required.", new string[] { nameof(PaymentToken) });
        }
        if (PlanType != PlanType.Free && !PaymentMethodType.HasValue)
        {
            yield return new ValidationResult("Payment method type required.",
                new string[] { nameof(PaymentMethodType) });
        }
        if (PlanType != PlanType.Free && string.IsNullOrWhiteSpace(BillingAddressCountry))
        {
            yield return new ValidationResult("Country required.",
                new string[] { nameof(BillingAddressCountry) });
        }
        if (PlanType != PlanType.Free && BillingAddressCountry == "US" &&
            string.IsNullOrWhiteSpace(BillingAddressPostalCode))
        {
            yield return new ValidationResult("Zip / postal code is required.",
                new string[] { nameof(BillingAddressPostalCode) });
        }
    }
}
