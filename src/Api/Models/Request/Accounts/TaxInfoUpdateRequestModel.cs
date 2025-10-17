// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core;

namespace Bit.Api.Models.Request.Accounts;

public class TaxInfoUpdateRequestModel : IValidatableObject
{
    [Required]
    public string Country { get; set; }
    public string PostalCode { get; set; }

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Country == Constants.CountryAbbreviations.UnitedStates && string.IsNullOrWhiteSpace(PostalCode))
        {
            yield return new ValidationResult("Zip / postal code is required.",
                new string[] { nameof(PostalCode) });
        }
    }
}
