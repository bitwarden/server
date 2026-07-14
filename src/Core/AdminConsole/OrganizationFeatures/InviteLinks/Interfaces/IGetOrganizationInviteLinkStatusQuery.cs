using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

public interface IGetOrganizationInviteLinkStatusQuery
{
    /// <summary>
    /// Gets the status of an invite link by its organization ID and code, for display to an anonymous user.
    /// </summary>
    /// <param name="organizationId">The organization's ID (from the URL path).</param>
    /// <param name="code">The public invite link code (bearer secret from the URL).</param>
    /// <returns>
    /// An <see cref="OrganizationInviteLinkStatus"/> if the link is valid, or an error if the link is not
    /// found or the organization is disabled. The status indicates whether the invite links feature is enabled.
    /// </returns>
    Task<CommandResult<OrganizationInviteLinkStatus>> GetStatusAsync(Guid organizationId, Guid code);
}
