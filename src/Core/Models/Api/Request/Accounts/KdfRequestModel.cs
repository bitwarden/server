using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api
{
    public class KdfRequestModel : PasswordRequestModel, IValidatableObject
    {
        [Required]
        public KdfType? Kdf { get; set; }
        [Required]
        public int? KdfIterations { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Kdf.HasValue && KdfIterations.HasValue)
            {
                switch (Kdf.Value)
                {
                    case KdfType.PBKDF2_SHA256:
                        if (KdfIterations.Value < 5000 || KdfIterations.Value > 2_000_000)
                        {
                            yield return new ValidationResult("KDF iterations must be between 5000 and 2000000.");
                        }
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
