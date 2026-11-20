using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface ISendOrganizationAcceptedEmailCommand
{
    /// <summary>
    /// Emails organization admins and owners that a user has accepted their invitation.
    /// </summary>
    Task SendAsync(Organization organization, string acceptedUserEmail);
}
