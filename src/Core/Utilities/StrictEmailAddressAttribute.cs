using System.ComponentModel.DataAnnotations;
using MimeKit;

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

            try
            {
                var parsedEmailAddress = MailboxAddress.Parse(emailAddress).Address;
                if (parsedEmailAddress != emailAddress)
                {
                    return false;
                }
            }
            catch (ParseException e)
            {
                return false;
            }

            return new EmailAddressAttribute().IsValid(emailAddress);
        }
    }
}
