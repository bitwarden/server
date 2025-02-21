using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Helpers;

public static class InviteScimOrganizationUserRequestHelpers
{
    public static InviteScimOrganizationUserRequest GetInviteScimOrganizationUserRequestDefault(string email,
        OrganizationDto organizationDto, DateTimeOffset performedAt, string externalId) =>
        InviteScimOrganizationUserRequest.Create(
            OrganizationUserSingleEmailInvite.Create(
                email,
                [],
                OrganizationUserType.User,
                new Permissions(),
                false),
            organizationDto,
            performedAt,
            externalId
        );
}
