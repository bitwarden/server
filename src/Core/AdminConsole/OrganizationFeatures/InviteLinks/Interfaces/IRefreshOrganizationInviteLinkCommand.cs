using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

public interface IRefreshOrganizationInviteLinkCommand
{
    /// <summary>
    /// Refreshes the invite link for the specified organization by replacing the existing link
    /// with a new one (new <c>Id</c>, <c>Code</c>, and <c>EncryptedInviteKey</c>). The existing
    /// <c>AllowedDomains</c> carry over. The previous link's URL stops working immediately.
    /// </summary>
    /// <param name="request">The organization id and the new encrypted invite key.</param>
    /// <returns>
    /// The new <see cref="OrganizationInviteLink"/>, or an error if the invite link feature is
    /// not available for the organization or no existing invite link is found.
    /// </returns>
    Task<CommandResult<OrganizationInviteLink>> RefreshAsync(RefreshOrganizationInviteLinkRequest request);
}
