using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class StorageRequestModel : IValidatableObject
    {
        [Required]
        public short? StorageGbAdjustment { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StorageGbAdjustment == 0)
            {
                yield return new ValidationResult("Storage adjustment cannot be 0.",
                    new string[] { nameof(StorageGbAdjustment) });
            }
        }
    }
}
