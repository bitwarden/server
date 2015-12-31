using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models
{
    public class UpdateTwoFactorRequestModel : IValidatableObject
    {
        [Required]
        public string MasterPasswordHash { get; set; }
        [Required]
        public bool? Enabled { get; set; }
        [StringLength(50)]
        public string Token { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if(Enabled.HasValue && Enabled.Value && string.IsNullOrWhiteSpace(Token))
            {
                yield return new ValidationResult("Token is required.", new[] { "Token" });
            }
        }
    }
}
