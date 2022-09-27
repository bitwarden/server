using System.ComponentModel.DataAnnotations;
using Bit.Core.Settings;
using Enums = Bit.Core.Enums;

namespace Bit.Api.Models.Request.Accounts;

public class PremiumRequestModel : IValidatableObject
{
    [Required]
    public Enums.PaymentMethodType? PaymentMethodType { get; set; }
    public string PaymentToken { get; set; }
    [Range(0, 99)]
    public short? AdditionalStorageGb { get; set; }
    public IFormFile License { get; set; }
    public string Country { get; set; }
    public string PostalCode { get; set; }

    public bool Validate(GlobalSettings globalSettings)
    {
        if (!(License == null && !globalSettings.SelfHosted) ||
            (License != null && globalSettings.SelfHosted))
        {
            return false;
        }
        return globalSettings.SelfHosted || !string.IsNullOrWhiteSpace(Country);
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var creditType = PaymentMethodType.HasValue && PaymentMethodType.Value == Enums.PaymentMethodType.Credit;
        if (string.IsNullOrWhiteSpace(PaymentToken) && !creditType && License == null)
        {
            yield return new ValidationResult("Payment token or license is required.");
        }
        if (Country == "US" && string.IsNullOrWhiteSpace(PostalCode))
        {
            yield return new ValidationResult("Zip / postal code is required.",
                new string[] { nameof(PostalCode) });
        }
    }
}
