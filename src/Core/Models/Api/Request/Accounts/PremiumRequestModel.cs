using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api
{
    public class PremiumRequestModel : IValidatableObject
    {
        [Required]
        public PaymentMethodType? PaymentMethodType { get; set; }
        public string PaymentToken { get; set; }
        [Range(0, 99)]
        public short? AdditionalStorageGb { get; set; }
        public IFormFile License { get; set; }

        public bool Validate(GlobalSettings globalSettings)
        {
            return (License == null && !globalSettings.SelfHosted) ||
                (License != null && globalSettings.SelfHosted);
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var creditType = PaymentMethodType.HasValue && PaymentMethodType.Value == Enums.PaymentMethodType.Credit;
            if(string.IsNullOrWhiteSpace(PaymentToken) && !creditType && License == null)
            {
                yield return new ValidationResult("Payment token or license is required.");
            }
        }
    }
}
