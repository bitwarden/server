using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public static class CreateOrganizationUserExtensions
{
    public static CreateOrganizationUser MapToDataModel(this OrganizationUserInviteCommandModel organizationUserInvite,
        DateTimeOffset performedAt,
        InviteOrganization organization)
    {
        var orgUser = OrganizationUser.CreateInvited(
            organization.OrganizationId,
            organizationUserInvite.Email,
            organizationUserInvite.Type,
            string.IsNullOrWhiteSpace(organizationUserInvite.ExternalId) ? null : organizationUserInvite.ExternalId,
            performedAt);
        orgUser.AccessSecretsManager = organizationUserInvite.AccessSecretsManager;

        if (organizationUserInvite.Type == OrganizationUserType.Custom)
        {
            orgUser.SetPermissions(organizationUserInvite.Permissions);
        }

        return new CreateOrganizationUser
        {
            OrganizationUser = orgUser,
            Collections = organizationUserInvite.AssignedCollections,
            Groups = organizationUserInvite.Groups
        };
    }
}
