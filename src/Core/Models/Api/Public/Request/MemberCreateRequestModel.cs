using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api.Public
{
    public class MemberCreateRequestModel : MemberUpdateRequestModel, IValidatableObject
    {
        /// <summary>
        /// The member's email address.
        /// </summary>
        /// <example>jsmith@example.com</example>
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        public override OrganizationUser ToOrganizationUser(OrganizationUser existingUser)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if(Email.Contains(" ") || Email.Contains("<"))
            {
                yield return new ValidationResult($"Email is not valid.",
                    new string[] { nameof(Email) });
            }
            else if(Email.Length > 50)
            {
                yield return new ValidationResult($"Email is longer than 50 characters.",
                    new string[] { nameof(Email) });
            }
        }
    }
}
