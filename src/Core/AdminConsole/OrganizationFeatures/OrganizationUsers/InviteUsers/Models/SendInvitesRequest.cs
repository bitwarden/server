using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class SendInvitesRequest
{
    public SendInvitesRequest() { }

    public SendInvitesRequest(IEnumerable<OrganizationUser> users, Organization organization) =>
        (Users, Organization) = (users.ToArray(), organization);

    public OrganizationUser[] Users { get; set; } = [];
    public Organization Organization { get; set; } = null!;
}
