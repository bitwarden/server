// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
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
            Emails = new[] { Email },
            Type = Type.Value,
            Collections = Collections?.Select(c => c.ToCollectionAccessSelection())?.ToList() ?? [],
            Groups = Groups
        };

        // Permissions property is optional for backwards compatibility with existing usage
        if (Type is OrganizationUserType.Custom && Permissions is not null)
        {
            invite.Permissions = Permissions.ToData();
        }

        return invite;
    }

    public InviteOrganizationUsersRequest ToInviteRequest(
        Organization organization,
        bool accessSecretsManager,
        Guid performedBy,
        DateTimeOffset performedAt)
    {
        // Permissions property is optional for backwards compatibility with existing usage
        var permissions = (Type is OrganizationUserType.Custom && Permissions is not null)
            ? Permissions.ToData()
            : new Permissions();

        return new InviteOrganizationUsersRequest(
            invites:
            [
                new OrganizationUserInviteCommandModel(
                    email: Email,
                    assignedCollections: Collections?.Select(c => c.ToCollectionAccessSelection()) ?? [],
                    groups: Groups ?? [],
                    type: Type!.Value,
                    permissions: permissions,
                    externalId: ExternalId,
                    accessSecretsManager: accessSecretsManager)
            ],
            organization: organization,
            performedBy: performedBy,
            performedAt: performedAt);
    }
}
