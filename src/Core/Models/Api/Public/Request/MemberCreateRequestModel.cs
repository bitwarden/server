using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Table;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Api.Public
{
    public class MemberCreateRequestModel : MemberUpdateRequestModel, IValidatableObject
    {
        /// <summary>
        /// The member's email address.
        /// </summary>
        /// <example>jsmith@example.com</example>
        [Required]
        [StrictEmailAddress]
        public string Email { get; set; }

        public override OrganizationUser ToOrganizationUser(OrganizationUser existingUser)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var strictEmailAttribute = new StrictEmailAddressAttribute();
            if (!strictEmailAttribute.IsValid(Email))
            {
                yield return new ValidationResult($"Email is not valid.",
                    new string[] { nameof(Email) });
            }
            else if (Email.Length > 256)
            {
                yield return new ValidationResult($"Email is longer than 256 characters.",
                    new string[] { nameof(Email) });
            }
        }
    }
}
