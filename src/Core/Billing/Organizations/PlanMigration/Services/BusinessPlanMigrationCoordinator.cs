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
                    "Business plan migration scheduled for Organization ({OrganizationId}) subscription ({SubscriptionId}) but reloading the cohort assignment failed; renewal notification not sent, manual notification may be required",
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

        // Notify phase: the schedule is already committed, so failures here are caught and never propagate.
        // We send first, then stamp.
        if (assignment.RenewalNotificationSentDate is null)
        {
            bool notificationSent;
            try
            {
                var cohort = await cohortRepository.GetByIdAsync(assignment.CohortId);
                notificationSent = await renewalNotificationService.SendRenewalEmailAsync(organization, subscription, cohort);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Business plan migration scheduled for Organization ({OrganizationId}) subscription ({SubscriptionId}) but the renewal notification did not complete; a later sweep run will retry",
                    organization.Id, subscription.Id);
                return BusinessPlanMigrationResult.CompletedWithoutNotification;
            }

            if (!notificationSent)
            {
                return BusinessPlanMigrationResult.CompletedWithoutNotification;
            }

            try
            {
                var stampedAt = DateTime.UtcNow;
                assignment.RenewalNotificationSentDate = stampedAt;
                assignment.RevisionDate = stampedAt;
                await cohortAssignmentRepository.ReplaceAsync(assignment);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Renewal email was sent to Organization ({OrganizationId}) but stamping RenewalNotificationSentDate on cohort assignment ({CohortId}) failed; a later sweep run may resend",
                    organization.Id, assignment.CohortId);
            }
        }

        return BusinessPlanMigrationResult.Completed;
    }
}
