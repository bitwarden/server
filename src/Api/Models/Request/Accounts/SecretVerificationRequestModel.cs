using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Request.Accounts
{
    public class SecretVerificationRequestModel : IValidatableObject
    {
        [StringLength(300)]
        public string MasterPasswordHash { get; set; }
        public string OTP { get; set; }
        public string Secret => !string.IsNullOrEmpty(MasterPasswordHash) ? MasterPasswordHash : OTP;

        public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrEmpty(Secret))
            {
                yield return new ValidationResult("MasterPasswordHash or OTP must be supplied.");
            }
        }
    }
}
