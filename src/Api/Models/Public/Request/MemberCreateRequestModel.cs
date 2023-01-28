using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Public.Request;

public class MemberCreateRequestModel : MemberUpdateRequestModel
{
    /// <summary>
    /// The member's email address.
    /// </summary>
    /// <example>jsmith@example.com</example>
    [Required]
    [StringLength(256)]
    [StrictEmailAddress]
    public string Email { get; set; }

    public override OrganizationUser ToOrganizationUser(OrganizationUser existingUser)
    {
        throw new NotImplementedException();
    }
}
