using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

public interface IGetOrganizationInviteLinkQuery
{
    /// <summary>
    /// Gets the invite link for the specified organization.
    /// </summary>
    /// <param name="organizationId">The organization to get the invite link for.</param>
    /// <returns>
    /// The <see cref="OrganizationInviteLink"/> if found and available, or an error if the invite link
    /// feature is not available or no invite link exists.
    /// </returns>
    Task<CommandResult<OrganizationInviteLink>> GetAsync(Guid organizationId);
}
