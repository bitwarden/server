using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

public interface IGetOrganizationInviteLinkPoliciesQuery
{
    /// <summary>
    /// Gets the enabled policies for an organization by invite link, for display to an anonymous user.
    /// </summary>
    /// <param name="organizationId">The organization's ID (from the URL path).</param>
    /// <param name="code">The public invite link code (bearer secret from the URL).</param>
    /// <returns>
    /// The enabled <see cref="Policy"/> records for the organization if the link is valid, or an error
    /// if the link is not found, the organization is disabled, or the feature / policies are unavailable.
    /// </returns>
    Task<CommandResult<ICollection<Policy>>> GetPoliciesAsync(Guid organizationId, Guid code);
}
