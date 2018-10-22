using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;
using System.Linq;

namespace Bit.Core.Models.Api
{
    public class OrganizationUserInviteRequestModel : IValidatableObject
    {
        [Required]
        public IEnumerable<string> Emails { get; set; }
        [Required]
        public Enums.OrganizationUserType? Type { get; set; }
        public bool AccessAll { get; set; }
        public IEnumerable<SelectionReadOnlyRequestModel> Collections { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if(!Emails.Any())
            {
                yield return new ValidationResult("An email is required.");
            }

            if(Emails.Count() > 20)
            {
                yield return new ValidationResult("You can only invite up to 20 users at a time.");
            }

            var attr = new EmailAddressAttribute();
            for(var i = 0; i < Emails.Count(); i++)
            {
                var email = Emails.ElementAt(i);
                if(!attr.IsValid(email))
                {
                    yield return new ValidationResult($"Email #{i + 1} is not valid.", new string[] { nameof(Emails) });
                }
                else if(email.Length > 50)
                {
                    yield return new ValidationResult($"Email #{i + 1} is longer than 50 characters.", new string[] { nameof(Emails) });
                }
            }
        }
    }

    public class OrganizationUserAcceptRequestModel
    {
        [Required]
        public string Token { get; set; }
    }

    public class OrganizationUserConfirmRequestModel
    {
        [Required]
        public string Key { get; set; }
    }

    public class OrganizationUserUpdateRequestModel
    {
        [Required]
        public Enums.OrganizationUserType? Type { get; set; }
        public bool AccessAll { get; set; }
        public IEnumerable<SelectionReadOnlyRequestModel> Collections { get; set; }

        public OrganizationUser ToOrganizationUser(OrganizationUser existingUser)
        {
            existingUser.Type = Type.Value;
            existingUser.AccessAll = AccessAll;
            return existingUser;
        }
    }

    public class OrganizationUserUpdateGroupsRequestModel
    {
        [Required]
        public IEnumerable<string> GroupIds { get; set; }
    }
}
