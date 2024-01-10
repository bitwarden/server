using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
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

    public OrganizationUserInvite ToOrganizationUserInvite()
    {
        var invite = new OrganizationUserInvite
        {
            Emails = new List<string>() { Email },
            Type = Type.Value,
            AccessAll = AccessAll.Value,
            Collections = Collections.Select(c => c.ToCollectionAccessSelection()),
            Groups = Groups,
        };

        if (Type is OrganizationUserType.Custom && Permissions is not null)
        {
            invite.Permissions = Permissions;
        }

        return invite;
    }
}
