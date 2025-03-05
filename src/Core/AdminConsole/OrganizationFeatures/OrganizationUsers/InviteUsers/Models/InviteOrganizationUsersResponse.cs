using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class InviteOrganizationUsersResponse
{
    public IEnumerable<OrganizationUser> InvitedUsers { get; set; } = [];
}

public class ScimInviteOrganizationUsersResponse
{
    public OrganizationUser InvitedUser { get; init; }

}
