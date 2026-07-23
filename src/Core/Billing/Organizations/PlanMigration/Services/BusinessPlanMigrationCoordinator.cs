using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Pricing;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Organizations.PlanMigration.Services;

public class BusinessPlanMigrationCoordinator(
    IOrganizationPlanMigrationCohortAssignmentRepository cohortAssignmentRepository,
    IOrganizationPlanMigrationCohortRepository cohortRepository,
    IPriceIncreaseScheduler priceIncreaseScheduler,
    IBusinessPlanRenewalNotificationService renewalNotificationService,
    ILogger<BusinessPlanMigrationCoordinator> logger)
    : IBusinessPlanMigrationCoordinator
{
    public async Task<BusinessPlanMigrationResult> ExecuteAsync(
        Organization organization, Subscription subscription)
    {
        var assignment = await cohortAssignmentRepository.GetByOrganizationIdAsync(organization.Id);
        if (assignment is null)
        {
            return BusinessPlanMigrationResult.NotAssigned;
        }

        if (assignment.MigratedDate is not null)
        {
            return BusinessPlanMigrationResult.AlreadyMigrated;
        }

        // Scheduling phase: unexpected exceptions propagate to the caller's error boundary.
        if (assignment.ScheduledDate is null)
        {
            var scheduled = await priceIncreaseScheduler.ScheduleForSubscription(subscription);
            if (!scheduled)
            {
                return BusinessPlanMigrationResult.NotScheduled;
            }

            // Re-load so ReplaceAsync stamps the scheduler's committed copy instead of nulling ScheduledDate.
            // The schedule is committed, so a reload failure resolves to CompletedWithoutNotification, not a throw.
            try
            {
                assignment = await cohortAssignmentRepository.GetByOrganizationIdAsync(organization.Id);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Business plan migration was scheduled for Organization ({OrganizationId}) subscription ({SubscriptionId}), but reloading the cohort assignment failed; the schedule is committed, so this will retry on a later run",
                    organization.Id, subscription.Id);
                return BusinessPlanMigrationResult.CompletedWithoutNotification;
            }

            if (assignment is null)
            {
                // Schedule is committed but the assignment row is gone (drift, logged by the scheduler).
                // We must still suppress the standard email; we just can't record a notification.
                return BusinessPlanMigrationResult.CompletedWithoutNotification;
            }
        }

        // Notify phase: the schedule is committed, so failures here are caught (never fall back to the
        // standard-email path) and leave the stamp null so a later run retries.
        if (assignment.RenewalNotificationSentDate is null)
        {
            try
            {
                var cohort = await cohortRepository.GetByIdAsync(assignment.CohortId);
                var notificationSent = await renewalNotificationService.SendRenewalEmailAsync(organization, subscription, cohort);
                if (!notificationSent)
                {
                    return BusinessPlanMigrationResult.CompletedWithoutNotification;
                }

                assignment.RenewalNotificationSentDate = DateTime.UtcNow;
                await cohortAssignmentRepository.ReplaceAsync(assignment);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Business renewal email failed for Organization ({OrganizationId}) subscription ({SubscriptionId}); the schedule is committed, so this will retry on a later run",
                    organization.Id, subscription.Id);
                return BusinessPlanMigrationResult.CompletedWithoutNotification;
            }
        }

        return BusinessPlanMigrationResult.Completed;
    }
}
