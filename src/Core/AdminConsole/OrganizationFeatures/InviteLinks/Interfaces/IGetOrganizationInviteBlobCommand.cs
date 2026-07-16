using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Retrieves the opaque invite blob for an invite link after validating that the link exists, its
/// organization is enabled and supports invite links, and the user's email domain is allowed.
/// </summary>
public interface IGetOrganizationInviteBlobCommand
{
    Task<CommandResult<string>> GetInviteBlobAsync(GetOrganizationInviteBlobRequest request);
}
