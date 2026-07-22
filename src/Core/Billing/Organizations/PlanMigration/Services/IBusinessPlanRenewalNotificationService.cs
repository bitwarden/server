using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Stripe;

namespace Bit.Core.Billing.Organizations.PlanMigration.Services;

/// <summary>
/// Builds and sends the business-plan renewal email for a deferred price migration, computing the
/// quote from the target plan and cohort discounts.
/// </summary>
public interface IBusinessPlanRenewalNotificationService
{
    /// <summary>
    /// Sends the renewal email for the organization's migration.
    /// </summary>
    /// <param name="organization">The organization being migrated.</param>
    /// <param name="subscription">The organization's Stripe subscription.</param>
    /// <param name="cohort">The cohort the organization belongs to (may be null if the row is missing).</param>
    /// <returns>
    /// True if an email was sent; false if no email was sent because it could not be built
    /// (null/misconfigured cohort, unknown migration path, or an indeterminate renewal date).
    /// Throws only on unexpected failures (pricing, Stripe, or mailer errors).
    /// </returns>
    Task<bool> SendRenewalEmailAsync(
        Organization organization, Subscription subscription, OrganizationPlanMigrationCohort? cohort);
}
