using Bit.Core.AdminConsole.Models.Data.Organizations;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

public interface IGetOrganizationSubscriptionsToUpdateQuery
{
    /// <summary>
    /// Retrieves a collection of organization subscriptions that need to be updated. This is based on if the
    /// Organization.SyncSeats flag is true and Organization.Seats has a value.
    /// </summary>
    /// <returns>
    /// A collection of <see cref="OrganizationSubscriptionUpdate"/> instances, each representing an organization
    /// subscription to be updated with their associated plan.
    /// </returns>
    Task<IEnumerable<OrganizationSubscriptionUpdate>> GetOrganizationSubscriptionsToUpdateAsync();
}
