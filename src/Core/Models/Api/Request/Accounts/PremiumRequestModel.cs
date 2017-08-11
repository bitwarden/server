using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Bit.Core.Models.Api
{
    public class PremiumRequestModel : IValidatableObject
    {
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
            if(string.IsNullOrWhiteSpace(PaymentToken) && License == null)
            {
                yield return new ValidationResult("Payment token or license is required.");
            }
        }
    }
}
