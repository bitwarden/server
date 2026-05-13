using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

public interface IRefreshOrganizationInviteLinkCommand
{
    /// <summary>
    /// Replaces the invite link for the specified organization with a new one. The existing allowed domains carry over.
    /// </summary>
    /// <param name="request">The organization ID and the encryption keys.</param>
    /// <returns>The new <see cref="OrganizationInviteLink"/>, or an error if the feature is unavailable or no existing link is found.</returns>
    Task<CommandResult<OrganizationInviteLink>> RefreshAsync(RefreshOrganizationInviteLinkRequest request);
}
