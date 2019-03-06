using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api.Public
{
    public class MemberCreateRequestModel : MemberUpdateRequestModel
    {
        /// <summary>
        /// The member's email address.
        /// </summary>
        /// <example>jsmith@company.com</example>
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        public override OrganizationUser ToOrganizationUser(OrganizationUser existingUser)
        {
            throw new NotImplementedException();
        }
    }
}
