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

    public ScimInviteOrganizationUsersResponse(InviteOrganizationUsersRequest request)
    {
        var userToInvite = request.Invites.First();

        InvitedUser = new OrganizationUser
        {
            Email = userToInvite.Email,
            ExternalId = userToInvite.ExternalId
        };
    }
}
