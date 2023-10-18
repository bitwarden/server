using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Public.Models.Request;

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
