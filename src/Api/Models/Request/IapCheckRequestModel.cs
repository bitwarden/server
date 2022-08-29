using System.ComponentModel.DataAnnotations;
using Enums = Bit.Core.Enums;

namespace Bit.Api.Models.Request;

public class IapCheckRequestModel : IValidatableObject
{
    [Required]
    public Enums.PaymentMethodType? PaymentMethodType { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PaymentMethodType != Enums.PaymentMethodType.AppleInApp)
        {
            yield return new ValidationResult("Not a supported in-app purchase payment method.",
                new string[] { nameof(PaymentMethodType) });
        }
    }
}
