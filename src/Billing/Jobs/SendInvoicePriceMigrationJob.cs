using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.Services;
using Bit.Core.Billing.Services;
using Bit.Core.Jobs;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Quartz;
using Stripe;

namespace Bit.Billing.Jobs;

/// <summary>
/// Daily sweep that runs the business-plan price migration for organizations paying by invoice
/// (<see cref="StripeConstants.CollectionMethod.SendInvoice"/>). Stripe never emits an
/// <c>invoice.upcoming</c> event for these subscriptions, so the upcoming-invoice webhook handler
/// never fires for them (PM-38728) and they would otherwise renew at the old price.
/// </summary>
/// <remarks>
/// Scheduling, renewal notification, and idempotency live in
/// <see cref="IBusinessPlanMigrationCoordinator"/>; email retry emerges from re-running this sweep,
/// since selection keeps assignments without a RenewalNotificationSentDate eligible while the
/// organization's expiration stays inside the window. This job only selects candidates and re-verifies
/// their eligibility against the live Stripe subscription before delegating.
/// </remarks>
[DisallowConcurrentExecution]
public class SendInvoicePriceMigrationJob(
    IOrganizationPlanMigrationCohortAssignmentRepository cohortAssignmentRepository,
    IOrganizationPlanMigrationCohortRepository cohortRepository,
    IOrganizationRepository organizationRepository,
    IStripeAdapter stripeAdapter,
    IBusinessPlanMigrationCoordinator businessPlanMigrationCoordinator,
    IFeatureService featureService,
    ILogger<SendInvoicePriceMigrationJob> logger) : BaseJob(logger)
{
    // The business target is ~15 days' notice ahead of the renewal invoice — parity with the
    // charge_automatically upcoming-invoice email (PM-38728). 7 days is the floor under which there
    // is no longer enough lead time to schedule and notify within this renewal cycle.
    private const int LeadTimeDays = 15;
    private const int MinimumLeadTimeDays = 7;

    private const int MaxConcurrency = 10;

    public static ITrigger GetTrigger() =>
        TriggerBuilder.Create()
            .WithIdentity("SendInvoicePriceMigrationTrigger")
            .StartNow()
            .WithCronSchedule("0 0 8 * * ?")
            .Build();

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.PM38728_SendInvoicePriceMigration))
        {
            // send_invoice organizations have no webhook fallback, so leave a daily trace that the
            // sweep is alive but gated rather than silently doing nothing.
            _logger.LogInformation(
                "Send-invoice price migration sweep skipped: feature flag {FeatureFlag} is disabled",
                FeatureFlagKeys.PM38728_SendInvoicePriceMigration);
            return;
        }

        var candidates =
            await cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(MinimumLeadTimeDays, LeadTimeDays);

        _logger.LogInformation(
            "Send-invoice price migration sweep found {CandidateCount} candidates renewing between {MinimumLeadTimeDays} and {LeadTimeDays} days from now",
            candidates.Count, MinimumLeadTimeDays, LeadTimeDays);

        using var semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
        var processed = 0;
        var skipped = 0;
        var errors = 0;

        try
        {
            await Task.WhenAll(candidates.Select(async assignment =>
            {
                await semaphore.WaitAsync(context.CancellationToken);
                try
                {
                    // Per-item boundary: the coordinator's scheduling phase propagates exceptions, and one
                    // organization's failure must not abandon the rest of the sweep.
                    var migrated = await ProcessAssignmentAsync(assignment);
                    if (migrated)
                    {
                        Interlocked.Increment(ref processed);
                    }
                    else
                    {
                        Interlocked.Increment(ref skipped);
                    }
                }
                catch (StripeException exception)
                {
                    Interlocked.Increment(ref errors);
                    _logger.LogError(
                        exception,
                        "Send-invoice price migration failed for Organization ({OrganizationId}) cohort assignment ({AssignmentId}): Stripe error '{StripeErrorCode}'",
                        assignment.OrganizationId, assignment.Id, exception.StripeError?.Code);
                }
                catch (Exception exception)
                {
                    Interlocked.Increment(ref errors);
                    _logger.LogError(
                        exception,
                        "Send-invoice price migration failed for Organization ({OrganizationId}) cohort assignment ({AssignmentId})",
                        assignment.OrganizationId, assignment.Id);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // Routine shutdown, not a sweep failure: record the partial counters off the error channel.
            // Unprocessed candidates are re-selected by tomorrow's run.
            _logger.LogWarning(
                "Send-invoice price migration sweep cancelled during shutdown. Migrated: {Processed}, Skipped: {Skipped}, Errors: {Errors}",
                processed, skipped, errors);
            return;
        }

        _logger.LogInformation(
            "Send-invoice price migration sweep complete. Migrated: {Processed}, Skipped: {Skipped}, Errors: {Errors}",
            processed, skipped, errors);
    }

    private async Task<bool> ProcessAssignmentAsync(OrganizationPlanMigrationCohortAssignment assignment)
    {
        var organization = await organizationRepository.GetByIdAsync(assignment.OrganizationId);

        if (organization == null)
        {
            _logger.LogWarning(
                "Organization ({OrganizationId}) for cohort assignment ({AssignmentId}) no longer exists; skipping send-invoice price migration",
                assignment.OrganizationId, assignment.Id);
            return false;
        }

        if (string.IsNullOrEmpty(organization.GatewaySubscriptionId))
        {
            _logger.LogWarning(
                "Organization ({OrganizationId}) has no gateway subscription; skipping send-invoice price migration",
                organization.Id);
            return false;
        }

        // Selection filters on cohort activity but not migration path, so churn-only cohorts (null
        // MigrationPathId) reach this sweep. They have nothing to schedule and must not receive a
        // renewal price-change email; skip them before spending a Stripe fetch.
        var cohort = await cohortRepository.GetByIdAsync(assignment.CohortId);

        if (cohort == null)
        {
            _logger.LogWarning(
                "Cohort ({CohortId}) for cohort assignment ({AssignmentId}) no longer exists; skipping send-invoice price migration",
                assignment.CohortId, assignment.Id);
            return false;
        }

        if (cohort.MigrationPathId == null)
        {
            _logger.LogInformation(
                "Organization ({OrganizationId}) belongs to churn-only cohort ({CohortId}); skipping send-invoice price migration",
                organization.Id, cohort.Id);
            return false;
        }

        /*
         * These expansions are load-bearing: the price increase scheduler reads Discounts[].Source.Coupon.Id and
         * Customer.Discount.Source.Coupon(customer.discount implies customer)  to carry existing discounts onto the new
         * schedule phases, and the eligibility window check and the renewal notification service
         * read TestClock.FrozenTime so test-clock subscriptions evaluate against their frozen time.
         */
        var subscription = await stripeAdapter.GetSubscriptionAsync(
            organization.GatewaySubscriptionId,
            new SubscriptionGetOptions
            {
                Expand = ["discounts.source.coupon", "customer.discount.source.coupon", "test_clock"]
            });

        if (!IsStillEligible(organization, subscription))
        {
            return false;
        }

        var result = await businessPlanMigrationCoordinator.ExecuteAsync(organization, subscription);
        return LogResult(organization, subscription, result);
    }

    /// <summary>
    /// Re-verifies eligibility against the live subscription. Selection reads
    /// <see cref="Organization.ExpirationDate"/>, which is editable from the Admin Portal and can drift
    /// from Stripe, and cannot see collection method or cancellation state at all (neither is stored
    /// locally), so all of it is confirmed here before committing.
    /// </summary>
    private bool IsStillEligible(Organization organization, Subscription subscription)
    {
        if (subscription.CollectionMethod != StripeConstants.CollectionMethod.SendInvoice)
        {
            _logger.LogInformation(
                "Subscription ({SubscriptionId}) for Organization ({OrganizationId}) no longer uses the '{CollectionMethod}' collection method; skipping send-invoice price migration",
                subscription.Id, organization.Id, StripeConstants.CollectionMethod.SendInvoice);
            return false;
        }

        if (subscription.CancelAtPeriodEnd ||
            subscription.CancelAt.HasValue ||
            subscription.Status is not (StripeConstants.SubscriptionStatus.Active
                or StripeConstants.SubscriptionStatus.Trialing
                or StripeConstants.SubscriptionStatus.PastDue))
        {
            _logger.LogInformation(
                "Subscription ({SubscriptionId}) for Organization ({OrganizationId}) is not renewing (status '{Status}', cancel at period end: {CancelAtPeriodEnd}, cancel at: {CancelAt}); skipping send-invoice price migration",
                subscription.Id, organization.Id, subscription.Status, subscription.CancelAtPeriodEnd,
                subscription.CancelAt);
            return false;
        }

        var currentPeriodEnd = subscription.GetCurrentPeriodEnd();

        if (currentPeriodEnd == null)
        {
            _logger.LogWarning(
                "Could not determine the current period end for Subscription ({SubscriptionId}) for Organization ({OrganizationId}); skipping send-invoice price migration",
                subscription.Id, organization.Id);
            return false;
        }

        // Under a Stripe test clock the renewal date is relative to the clock's frozen time, not
        // wall-clock time; compare like-for-like so test-clock subscriptions stay selectable.
        var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

        if (currentPeriodEnd < now.AddDays(MinimumLeadTimeDays))
        {
            // Unrecoverable this cycle: the renewal only gets closer, so this organization can never
            // re-enter the window before it renews at the old price. Error so it pages the same way
            // the migrated-but-not-notified state does — both mean a customer needs manual follow-up.
            _logger.LogError(
                "Subscription ({SubscriptionId}) for Organization ({OrganizationId}) renews on {RenewalDate}, closer than the {MinimumLeadTimeDays} day minimum notice period; it cannot be migrated this cycle",
                subscription.Id, organization.Id, currentPeriodEnd, MinimumLeadTimeDays);
            return false;
        }

        if (currentPeriodEnd > now.AddDays(LeadTimeDays))
        {
            _logger.LogInformation(
                "Subscription ({SubscriptionId}) for Organization ({OrganizationId}) renews on {RenewalDate}, more than {LeadTimeDays} days out; skipping until it enters the window",
                subscription.Id, organization.Id, currentPeriodEnd, LeadTimeDays);
            return false;
        }

        return true;
    }

    private bool LogResult(
        Organization organization,
        Subscription subscription,
        BusinessPlanMigrationResult result)
    {
        switch (result)
        {
            case BusinessPlanMigrationResult.Completed:
                _logger.LogInformation(
                    "Scheduled the business plan price migration for Organization ({OrganizationId}) subscription ({SubscriptionId})",
                    organization.Id, subscription.Id);
                return true;
            case BusinessPlanMigrationResult.CompletedWithoutNotification:
                {
                    // Retry only happens while selection still sees the organization, i.e. while the
                    // renewal stays at least MinimumLeadTimeDays out. On the last eligible day, say so.
                    var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;
                    if (subscription.GetCurrentPeriodEnd() < now.AddDays(MinimumLeadTimeDays + 1))
                    {
                        _logger.LogError(
                            "Business plan price migration was scheduled for Organization ({OrganizationId}) subscription ({SubscriptionId}) but no renewal notification was sent, and the renewal is about to leave the retry window; manual notification is required",
                            organization.Id, subscription.Id);
                    }
                    else
                    {
                        _logger.LogError(
                            "Business plan price migration was scheduled for Organization ({OrganizationId}) subscription ({SubscriptionId}) but no renewal notification was sent; tomorrow's sweep will retry while the renewal stays at least {MinimumLeadTimeDays} days out",
                            organization.Id, subscription.Id, MinimumLeadTimeDays);
                    }

                    return true;
                }
            case BusinessPlanMigrationResult.NotScheduled:
                // A decline here can also mean a Stripe schedule was created on a previous run but the
                // ScheduledDate stamp failed afterwards — a half-state where the customer will be
                // migrated without ever being notified unless someone intervenes. Error so it alerts.
                _logger.LogError(
                    "The price increase scheduler declined the business plan price migration for Organization ({OrganizationId}) subscription ({SubscriptionId}); check that the {BusinessMigrationFlag} feature flag is enabled, the cohort is active, and no existing Stripe schedule is missing its ScheduledDate stamp",
                    organization.Id, subscription.Id, FeatureFlagKeys.PM35215_BusinessPlanPriceMigration);
                return false;
            case BusinessPlanMigrationResult.NotAssigned:
                _logger.LogWarning(
                    "The cohort assignment for Organization ({OrganizationId}) no longer exists; skipping send-invoice price migration",
                    organization.Id);
                return false;
            case BusinessPlanMigrationResult.AlreadyMigrated:
                _logger.LogInformation(
                    "Skipped the business plan price migration for Organization ({OrganizationId}) subscription ({SubscriptionId}): already migrated",
                    organization.Id, subscription.Id);
                return false;
            default:
                _logger.LogWarning(
                    "Unrecognized business plan migration result '{Result}' for Organization ({OrganizationId}) subscription ({SubscriptionId}); counted as skipped",
                    result, organization.Id, subscription.Id);
                return false;
        }
    }
}
