using Bit.Core.AdminConsole.Models.Data.Organizations;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

public interface IUpdateOrganizationSubscriptionCommand
{
    /// <summary>
    /// Attempts to update the subscription of all organizations that have had a subscription update.
    ///
    /// If successful, the Organization.SyncSeats flag will be set to false and Organization.RevisionDate will be set.
    ///
    /// In the event of a failure, it will log the failure and maybe be picked up in later runs.
    /// </summary>
    /// <param name="subscriptionsToUpdate">The collection of organization subscriptions to update.</param>
    Task UpdateOrganizationSubscriptionAsync(IEnumerable<OrganizationSubscriptionUpdate> subscriptionsToUpdate);
}
