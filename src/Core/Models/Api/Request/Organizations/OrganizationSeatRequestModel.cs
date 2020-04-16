using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class OrganizationSeatRequestModel : IValidatableObject
    {
        [Required]
        public int? SeatAdjustment { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (SeatAdjustment == 0)
            {
                yield return new ValidationResult("Seat adjustment cannot be 0.", new string[] { nameof(SeatAdjustment) });
            }
        }
    }
}
