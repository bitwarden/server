using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;

public interface ITransitionPartnershipEntitlementCommand
{
    /// <summary>
    /// Applies a lifecycle transition to an existing entitlement. A transition older than the last applied one
    /// is acknowledged without being applied.
    /// </summary>
    /// <returns>The transition outcome, or an <see cref="IPartnershipError"/>.</returns>
    Task<CommandResult<PartnershipEntitlementTransitionResult>> TransitionAsync(TransitionPartnershipEntitlementRequest request);
}
