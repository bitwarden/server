using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public static class CreateOrganizationUserExtensions
{
    public static CreateOrganizationUser MapToDataModel(this OrganizationUserInviteCommandModel organizationUserInvite,
        DateTimeOffset performedAt,
        InviteOrganization organization) =>
        new()
        {
            OrganizationUser = new OrganizationUser
            {
                Id = CoreHelpers.GenerateComb(),
                OrganizationId = organization.OrganizationId,
                Email = organizationUserInvite.Email.ToLowerInvariant(),
                Type = organizationUserInvite.Type,
                Status = OrganizationUserStatusType.Invited,
                AccessSecretsManager = organizationUserInvite.AccessSecretsManager,
                ExternalId = string.IsNullOrWhiteSpace(organizationUserInvite.ExternalId) ? null : organizationUserInvite.ExternalId,
                CreationDate = performedAt.UtcDateTime,
                RevisionDate = performedAt.UtcDateTime
            },
            Collections = organizationUserInvite.AssignedCollections,
            Groups = organizationUserInvite.Groups
        };
}
