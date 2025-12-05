// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Utilities;

public class StrictEmailAddressAttribute : ValidationAttribute
{
    public StrictEmailAddressAttribute()
        : base("The {0} field is not a supported e-mail address format.")
    { }

    public override bool IsValid(object value)
    {
        var emailAddress = value?.ToString() ?? string.Empty;

        return emailAddress.IsValidEmail() && new EmailAddressAttribute().IsValid(emailAddress);
    }
}
