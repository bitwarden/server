using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class VerifyPasswordRequestModel : IValidatableObject
    {
        [StringLength(300)]
        public string MasterPasswordHash { get; set; }
        public string OTP { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrEmpty(MasterPasswordHash) && string.IsNullOrEmpty(OTP))
            {
                yield return new ValidationResult("MasterPasswordHash or OTP must be supplied.");
            }
        }

        public bool SuppliedMasterPassword()
        {
            return !string.IsNullOrEmpty(MasterPasswordHash);
        }
    }
}
