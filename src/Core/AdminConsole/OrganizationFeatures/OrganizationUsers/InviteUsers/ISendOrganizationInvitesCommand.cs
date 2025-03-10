using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Entities;
using Bit.Core.Models.Mail;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

public interface ISendOrganizationInvitesCommand
{
    Task SendInvitesAsync(SendInvitesRequest request);

    Task<OrganizationInvitesInfo> BuildOrganizationInvitesInfoAsync(IEnumerable<OrganizationUser> orgUsers, Organization organization, bool initOrganization = false);
}
