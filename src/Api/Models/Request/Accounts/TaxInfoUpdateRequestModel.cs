using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Request.Accounts;

public class TaxInfoUpdateRequestModel : IValidatableObject
{
    [Required]
    public string Country { get; set; }
    public string PostalCode { get; set; }

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Country == "US" && string.IsNullOrWhiteSpace(PostalCode))
        {
            yield return new ValidationResult(
                "Zip / postal code is required.",
                new string[] { nameof(PostalCode) }
            );
        }
    }
}
