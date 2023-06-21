using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationSeatRequestModel : IValidatableObject
{
    [Required]
    public int? SeatAdjustment { get; set; }
    [Required]
    public BitwardenProductType BitwardenProductType { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SeatAdjustment == 0)
        {
            yield return new ValidationResult("Seat adjustment cannot be 0.", new string[] { nameof(SeatAdjustment) });
        }
    }
}
