using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class InviteOrganizationUsersResponse
{
    public IEnumerable<OrganizationUser> InvitedUsers { get; set; } = [];
}

public class ScimInviteOrganizationUsersResponse
{
    public OrganizationUser InvitedUser { get; init; }

    public ScimInviteOrganizationUsersResponse()
    {

    }

    public ScimInviteOrganizationUsersResponse(OrganizationUserSingleEmailInvite request)
    {
        InvitedUser = new OrganizationUser
        {
            Email = request.Email,
            ExternalId = request.ExternalId
        };
    }
}
