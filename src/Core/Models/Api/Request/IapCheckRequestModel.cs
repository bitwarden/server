using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api
{
    public class IapCheckRequestModel : IValidatableObject
    {
        [Required]
        public PaymentMethodType? PaymentMethodType { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (PaymentMethodType != Enums.PaymentMethodType.AppleInApp)
            {
                yield return new ValidationResult("Not a supported in-app purchase payment method.",
                    new string[] { nameof(PaymentMethodType) });
            }
        }
    }
}
