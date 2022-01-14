using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using MimeKit;

namespace Bit.Core.Utilities
{
    public class StrictEmailAddressAttribute : ValidationAttribute
    {
        /**
        The regex below is intended to catch edge cases that are not handled by the general parsing check above.
        This enforces the following rules:
        * Requires ASCII only in the local-part (code points 0-127)
        * Requires an @ symbol
        * Allows any char in second-level domain name, including unicode and symbols
        * Requires at least one period (.) separating SLD from TLD
        * Must end in a letter (including unicode)
        See the unit tests for examples of what is allowed.
        **/
        private static readonly Regex _emailFormatRegex = new Regex(@"[\x00-\x7F]+@.+\.\p{L}+$", RegexOptions.Compiled);
        private static readonly EmailAddressAttribute _emailAddressAttribute = new EmailAddressAttribute();

        public StrictEmailAddressAttribute()
            : base("The {0} field is not a supported e-mail address format.")
        { }

        public override bool IsValid(object value)
            => IsValidCore(value);

        public static bool IsValidCore(object value)
        {
            var emailAddress = value?.ToString();
            if (emailAddress == null)
            {
                return false;
            }

            try
            {
                var parsedEmailAddress = MailboxAddress.Parse(emailAddress).Address;
                if (parsedEmailAddress != emailAddress)
                {
                    return false;
                }
            }
            catch (ParseException)
            {
                return false;
            }

            
            if (!_emailFormatRegex.IsMatch(emailAddress))
            {
                return false;
            }

            return _emailAddressAttribute.IsValid(emailAddress);
        }
    }
}
