using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

public interface IGetOrganizationInviteLinkPoliciesQuery
{
    /// <summary>
    /// Gets the enabled policies for an organization by invite link code, for display to an anonymous user.
    /// </summary>
    /// <param name="code">The public invite link code (from the URL).</param>
    /// <returns>
    /// The enabled <see cref="Policy"/> records for the organization if the link is valid, or an error
    /// if the link is not found, the organization is disabled, or the feature / policies are unavailable.
    /// </returns>
    Task<CommandResult<ICollection<Policy>>> GetPoliciesAsync(Guid code);
}
