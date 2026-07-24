using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Stripe;

namespace Bit.Core.Billing.Organizations.PlanMigration.Services;

/// <summary>
/// Coordinates the deferred business-plan price migration for a single organization: schedules the
/// price increase (if not already scheduled), then sends the renewal notification (if not already sent).
/// Idempotent across re-invocation.
/// </summary>
public interface IBusinessPlanMigrationCoordinator
{
    /// <summary>Runs the schedule → notify → stamp flow.</summary>
    /// <param name="organization">The organization to migrate.</param>
    /// <param name="subscription">The organization's Stripe subscription.</param>
    /// <returns>A <see cref="BusinessPlanMigrationResult"/> describing what happened.</returns>
    Task<BusinessPlanMigrationResult> ExecuteAsync(Organization organization, Subscription subscription);
}
