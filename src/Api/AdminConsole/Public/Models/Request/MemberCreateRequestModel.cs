using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;
using OrganizationUserInvite = Bit.Core.Models.Business.OrganizationUserInvite;

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
            Emails = new[] { Email },
            Type = Type.Value,
            Collections = Collections?.Select(c => c.ToCollectionAccessSelection()).ToList(),
            Groups = Groups
        };

        // Permissions property is optional for backwards compatibility with existing usage
        if (Type is OrganizationUserType.Custom && Permissions is not null)
        {
            invite.Permissions = Permissions.ToData();
        }

        return invite;
    }

    public OrganizationUserSingleEmailInvite ToOrganizationUserSingleEmailInvite(bool hasSecretsManager) =>
        OrganizationUserSingleEmailInvite.Create(
            Email,
            Collections.Select(c => c.ToCollectionAccessSelection()).ToArray(),
            string.Empty,
            Type.Value,
            Type is OrganizationUserType.Custom && Permissions is not null ? Permissions.ToData() : new Permissions(),
            hasSecretsManager);
}
