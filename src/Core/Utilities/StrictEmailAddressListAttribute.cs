using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Utilities;

#nullable enable

public class StrictEmailAddressListAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var strictEmailAttribute = new StrictEmailAddressAttribute();

        if (value is not IList<string> emails || emails.Count == 0)
        {
            return new ValidationResult("An email is required.");
        }

        if (emails.Count > 20)
        {
            return new ValidationResult("You can only submit up to 20 emails at a time.");
        }

        for (var i = 0; i < emails.Count; i++)
        {
            var email = emails[i];
            if (!strictEmailAttribute.IsValid(email))
            {
                return new ValidationResult($"Email #{i + 1} is not valid.");
            }

            if (email.Length > 256)
            {
                return new ValidationResult($"Email #{i + 1} is longer than 256 characters.");
            }
        }

        return ValidationResult.Success;
    }
}
