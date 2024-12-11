using System.ComponentModel.DataAnnotations;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationSeatRequestModel : IValidatableObject
{
    [Required]
    public int? SeatAdjustment { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SeatAdjustment == 0)
        {
            yield return new ValidationResult(
                "Seat adjustment cannot be 0.",
                new string[] { nameof(SeatAdjustment) }
            );
        }
    }
}
