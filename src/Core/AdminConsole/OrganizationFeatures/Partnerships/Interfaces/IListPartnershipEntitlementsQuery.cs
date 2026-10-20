using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;

public interface IListPartnershipEntitlementsQuery
{
    /// <summary>
    /// Reads one page of the partnership's entitlements in <c>(RevisionDate, Id)</c> order.
    /// </summary>
    /// <returns>The page, or <see cref="InvalidContinuationToken"/>.</returns>
    Task<CommandResult<PartnershipEntitlementPage>> ListAsync(ListPartnershipEntitlementsRequest request);
}
