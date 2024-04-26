using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
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

    public OrganizationUserInvite ToOrganizationUserInvite(bool flexibleCollectionsIsEnabled)
    {
        return new OrganizationUserInvite
        {
            Emails = new[] { Email },
            Type = Type.Value,
            AccessAll = AccessAll.Value,
            Collections = Collections?.Select(c => c.ToCollectionAccessSelection(flexibleCollectionsIsEnabled)).ToList(),
            Groups = Groups
        };

    }
}
