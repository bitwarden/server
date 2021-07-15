using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Bit.Core.Utilities
{
    public class StrictEmailAddressAttribute : ValidationAttribute
    {
        public StrictEmailAddressAttribute()
            : base("The {0} field is not a valid e-mail address.")
        {}
        
        public override bool IsValid(object value)
        {
            var emailAddress = value?.ToString();
            if (emailAddress == null)
            {
                return false;
            }

            var illegalChars = @"[\s<>()]";
            if (Regex.IsMatch(emailAddress, illegalChars))
            {
                return false;
            }
             
            return new EmailAddressAttribute().IsValid(emailAddress);
        }
    }
}
