using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

/// <summary>
/// This is for sending the invite to an organization user.
/// </summary>
public interface ISendOrganizationInvitesCommand
{
    /// <summary>
    /// This sends emails out to organization users for a given organization.
    /// </summary>
    /// <param name="request"><see cref="SendInvitesRequest"/></param>
    /// <returns></returns>
    Task SendInvitesAsync(SendInvitesRequest request);
}
