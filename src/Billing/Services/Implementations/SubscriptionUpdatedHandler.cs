using System.Globalization;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Organizations.Extensions;
using Bit.Core.Billing.Organizations.PlanMigration;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Core.Entities;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Stripe;
using static Bit.Core.Billing.Constants.StripeConstants;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class SubscriptionUpdatedHandler : ISubscriptionUpdatedHandler
{
    private readonly IStripeEventService _stripeEventService;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;
    private readonly IOrganizationService _organizationService;
    private readonly IStripeAdapter _stripeAdapter;
    private readonly IOrganizationSponsorshipRenewCommand _organizationSponsorshipRenewCommand;
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationEnableCommand _organizationEnableCommand;
    private readonly IOrganizationDisableCommand _organizationDisableCommand;
    private readonly IPricingClient _pricingClient;
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderService _providerService;
    private readonly IPushNotificationAdapter _pushNotificationAdapter;
    private readonly IPriceIncreaseScheduler _priceIncreaseScheduler;
    private readonly IFeatureService _featureService;
    private readonly IOrganizationPlanMigrationCohortRepository _cohortRepository;
    private readonly IOrganizationPlanMigrationCohortAssignmentRepository _cohortAssignmentRepository;
    private readonly ILogger<SubscriptionUpdatedHandler> _logger;

    public SubscriptionUpdatedHandler(
        IStripeEventService stripeEventService,
        IStripeEventUtilityService stripeEventUtilityService,
        IOrganizationService organizationService,
        IStripeAdapter stripeAdapter,
        IOrganizationSponsorshipRenewCommand organizationSponsorshipRenewCommand,
        IUserService userService,
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationEnableCommand organizationEnableCommand,
        IOrganizationDisableCommand organizationDisableCommand,
        IPricingClient pricingClient,
        IProviderRepository providerRepository,
        IProviderService providerService,
        IPushNotificationAdapter pushNotificationAdapter,
        IPriceIncreaseScheduler priceIncreaseScheduler,
        IFeatureService featureService,
        IOrganizationPlanMigrationCohortRepository cohortRepository,
        IOrganizationPlanMigrationCohortAssignmentRepository cohortAssignmentRepository,
        ILogger<SubscriptionUpdatedHandler> logger)
    {
        _stripeEventService = stripeEventService;
        _stripeEventUtilityService = stripeEventUtilityService;
        _organizationService = organizationService;
        _providerService = providerService;
        _stripeAdapter = stripeAdapter;
        _organizationSponsorshipRenewCommand = organizationSponsorshipRenewCommand;
        _userService = userService;
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _providerRepository = providerRepository;
        _organizationEnableCommand = organizationEnableCommand;
        _organizationDisableCommand = organizationDisableCommand;
        _pricingClient = pricingClient;
        _providerRepository = providerRepository;
        _providerService = providerService;
        _pushNotificationAdapter = pushNotificationAdapter;
        _priceIncreaseScheduler = priceIncreaseScheduler;
        _featureService = featureService;
        _cohortRepository = cohortRepository;
        _cohortAssignmentRepository = cohortAssignmentRepository;
        _logger = logger;
    }

    public async Task HandleAsync(Event parsedEvent)
    {
        var subscription = await _stripeEventService.GetSubscription(parsedEvent, true, ["customer.discount", "discounts", "latest_invoice", "test_clock"]);
        SubscriberId subscriberId = subscription;

        var subscriber = await GetSubscriberAsync(subscriberId);
        if (subscriber == null)
        {
            _logger.LogWarning(
                "Subscriber not found for subscription ({SubscriptionId}) in event ({EventId}), skipping handler",
                subscription.Id,
                parsedEvent.Id);
            return;
        }

        var currentPeriodEnd = subscription.GetCurrentPeriodEnd();
        var clearOrgBillingAutomationExemption = false;

        if (SubscriptionWentUnpaid(parsedEvent, subscription))
        {
            if (SkipUnpaidBillingAutomationsForExemptOrganization(subscriber))
            {
                _logger.LogInformation(
                    "Skipping billing automations for exempt organization ({OrganizationId}). Exemption will be cleared after handler completion",
                    subscriber.Id);
                clearOrgBillingAutomationExemption = true;
            }
            else
            {
                await DisableSubscriberAsync(subscriber, currentPeriodEnd);
                await SetSubscriptionToCancelAsync(subscription);
            }
        }
        else if (SubscriptionWentIncompleteExpired(parsedEvent, subscription))
        {
            // Subscription is already terminal in Stripe; any attempt to
            // schedule a cancel would be rejected and 500 the webhook,
            // causing Stripe to retry and re-run DisableSubscriberAsync.
            await DisableSubscriberAsync(subscriber, currentPeriodEnd);
        }
        else if (SubscriptionBecameActive(parsedEvent, subscription))
        {
            await EnableSubscriberAsync(subscriber, currentPeriodEnd);
            await RemovePendingCancellationAsync(subscription);
        }

        switch (subscriber)
        {
            case User user:
                await _userService.UpdatePremiumExpirationAsync(user.Id, currentPeriodEnd);
                break;
            case Organization organization:
                {
                    if (_featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal))
                    {
                        await HandleScheduleTriggeredFamiliesMigrationAsync(parsedEvent, subscription, organization.Id);
                    }

                    await HandleScheduleTriggeredBusinessMigrationAsync(parsedEvent, subscription, organization.Id);

                    await _organizationService.UpdateExpirationDateAsync(organization.Id, currentPeriodEnd);

                    if (_stripeEventUtilityService.IsSponsoredSubscription(subscription) && currentPeriodEnd.HasValue)
                    {
                        await _organizationSponsorshipRenewCommand.UpdateExpirationDateAsync(organization.Id, currentPeriodEnd.Value);
                    }

                    await RemovePasswordManagerCouponIfRemovingSecretsManagerTrialAsync(parsedEvent, subscription, organization.Id);

                    if (clearOrgBillingAutomationExemption)
                    {
                        await ClearBillingAutomationExemptionAsync(organization.Id);
                    }
                    break;
                }
        }
    }

    private static bool SubscriptionWentUnpaid(
        Event parsedEvent,
        Subscription currentSubscription) =>
        parsedEvent.Data.PreviousAttributes.ToObject<Subscription>() is Subscription
        {
            Status:
            SubscriptionStatus.Trialing or
            SubscriptionStatus.Active or
            SubscriptionStatus.PastDue
        } && currentSubscription is
        {
            Status: SubscriptionStatus.Unpaid,
            LatestInvoice.BillingReason: BillingReasons.SubscriptionCycle
        };

    private static bool SubscriptionWentIncompleteExpired(
        Event parsedEvent,
        Subscription currentSubscription) =>
        parsedEvent.Data.PreviousAttributes.ToObject<Subscription>() is Subscription
        {
            Status: SubscriptionStatus.Incomplete
        } && currentSubscription is
        {
            Status: SubscriptionStatus.IncompleteExpired,
            LatestInvoice.BillingReason: BillingReasons.SubscriptionCreate or BillingReasons.SubscriptionCycle
        };

    private static bool SubscriptionBecameActive(
        Event parsedEvent,
        Subscription currentSubscription) =>
        parsedEvent.Data.PreviousAttributes.ToObject<Subscription>() is Subscription
        {
            Status:
            SubscriptionStatus.Incomplete or
            SubscriptionStatus.Unpaid
        } && currentSubscription is
        {
            Status: SubscriptionStatus.Active,
            LatestInvoice.BillingReason: BillingReasons.SubscriptionCreate or BillingReasons.SubscriptionCycle
        };

    private static bool SkipUnpaidBillingAutomationsForExemptOrganization(ISubscriber subscriber) =>
        subscriber is Organization { Enabled: true, ExemptFromBillingAutomation: true };

    private Task<ISubscriber?> GetSubscriberAsync(SubscriberId subscriberId) =>
        subscriberId.Match<Task<ISubscriber?>>(
            async userId => await _userRepository.GetByIdAsync(userId.Value),
            async organizationId => await _organizationRepository.GetByIdAsync(organizationId.Value),
            async providerId => await _providerRepository.GetByIdAsync(providerId.Value));

    private Task DisableSubscriberAsync(ISubscriber subscriber, DateTime? currentPeriodEnd) =>
        subscriber switch
        {
            User user => DisableUserAsync(user.Id, currentPeriodEnd),
            Organization organization => DisableOrganizationAsync(organization.Id, currentPeriodEnd),
            Provider provider => DisableProviderAsync(provider.Id),
            _ => Task.CompletedTask
        };

    private async Task DisableUserAsync(Guid userId, DateTime? currentPeriodEnd)
    {
        await _userService.DisablePremiumAsync(userId, currentPeriodEnd);
        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            await _pushNotificationAdapter.NotifyPremiumStatusChangedAsync(user);
        }
    }

    private async Task DisableOrganizationAsync(Guid organizationId, DateTime? currentPeriodEnd)
    {
        await _organizationDisableCommand.DisableAsync(organizationId, currentPeriodEnd);
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization != null)
        {
            await _pushNotificationAdapter.NotifyEnabledChangedAsync(organization);
        }
    }

    private async Task DisableProviderAsync(Guid providerId)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider == null)
        {
            return;
        }

        provider.Enabled = false;
        await _providerService.UpdateAsync(provider);
    }

    private async Task ClearBillingAutomationExemptionAsync(Guid organizationId)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization == null)
        {
            return;
        }

        organization.ExemptFromBillingAutomation = false;
        organization.RevisionDate = DateTime.UtcNow;
        await _organizationRepository.ReplaceAsync(organization);

        _logger.LogInformation(
            "Exemption has been cleared for organization ({OrganizationId})",
            organizationId);
    }

    private Task EnableSubscriberAsync(ISubscriber subscriber, DateTime? currentPeriodEnd) =>
        subscriber switch
        {
            User user => EnableUserAsync(user.Id, currentPeriodEnd),
            Organization organization => EnableOrganizationAsync(organization.Id, currentPeriodEnd),
            Provider provider => EnableProviderAsync(provider.Id),
            _ => Task.CompletedTask
        };

    private async Task EnableUserAsync(Guid userId, DateTime? currentPeriodEnd)
    {
        await _userService.EnablePremiumAsync(userId, currentPeriodEnd);
        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            await _pushNotificationAdapter.NotifyPremiumStatusChangedAsync(user);
        }
    }

    private async Task EnableOrganizationAsync(Guid organizationId, DateTime? currentPeriodEnd)
    {
        await _organizationEnableCommand.EnableAsync(organizationId, currentPeriodEnd);
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization != null)
        {
            await _pushNotificationAdapter.NotifyEnabledChangedAsync(organization);
        }
    }

    private async Task EnableProviderAsync(Guid providerId)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider == null)
        {
            return;
        }

        provider.Enabled = true;
        await _providerService.UpdateAsync(provider);
    }

    private async Task SetSubscriptionToCancelAsync(Subscription subscription)
    {
        await _priceIncreaseScheduler.Release(subscription.CustomerId, subscription.Id);

        await _stripeAdapter.WaitForTestClockToAdvanceAsync(subscription.TestClock);

        var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

        await _stripeAdapter.UpdateSubscriptionAsync(subscription.Id, new SubscriptionUpdateOptions
        {
            CancelAt = now.AddDays(7),
            ProrationBehavior = ProrationBehavior.None,
            CancellationDetails = new SubscriptionCancellationDetailsOptions
            {
                Comment = $"Automation: Setting unpaid subscription to cancel 7 days from {now:yyyy-MM-dd}."
            },
            // Stamp the origin so SubscriptionDeletedHandler can recognize the eventual
            // customer.subscription.deleted as the tail of this platform-managed unpaid
            // lifecycle and void any open invoices. Other cancellation paths (voluntary,
            // off-platform, provider migration) intentionally do not set this.
            Metadata = new Dictionary<string, string>
            {
                [MetadataKeys.CancellationOrigin] = CancellationOrigins.UnpaidSubscription
            }
        });
    }

    private async Task RemovePendingCancellationAsync(Subscription subscription)
    {
        await _stripeAdapter.UpdateSubscriptionAsync(subscription.Id, new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = false,
            ProrationBehavior = ProrationBehavior.None,
            // Clear the origin marker — the customer paid the unpaid invoice and the
            // subscription is recovering. Stripe removes a metadata key when its value
            // is set to an empty string.
            Metadata = new Dictionary<string, string>
            {
                [MetadataKeys.CancellationOrigin] = string.Empty
            }
        });
        await _priceIncreaseScheduler.ScheduleForSubscription(subscription);
    }

    /// <summary>
    /// Removes the Password Manager coupon if the organization is removing the Secrets Manager trial.
    /// Only applies to organizations that have a subscription from the Secrets Manager trial.
    /// </summary>
    /// <param name="parsedEvent"></param>
    /// <param name="subscription"></param>
    /// <param name="organizationId"></param>
    private async Task RemovePasswordManagerCouponIfRemovingSecretsManagerTrialAsync(
        Event parsedEvent,
        Subscription subscription,
        Guid organizationId)
    {
        if (parsedEvent.Data.PreviousAttributes?.items is null)
        {
            return;
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization == null)
        {
            return;
        }

        var plan = await _pricingClient.GetPlanOrThrow(organization.PlanType);

        if (!plan.SupportsSecretsManager)
        {
            return;
        }

        var previousSubscription = parsedEvent.Data
            .PreviousAttributes
            .ToObject<Subscription>() as Subscription;

        // Get all plan IDs that include Secrets Manager support to check if the organization has secret manager in the
        // previous and/or current subscriptions.
        var planIdsOfPlansWithSecretManager = (await _pricingClient.ListPlans())
            .Where(orgPlan => orgPlan.SupportsSecretsManager && orgPlan.SecretsManager.StripeSeatPlanId != null)
            .Select(orgPlan => orgPlan.SecretsManager.StripeSeatPlanId)
            .ToHashSet();

        // This being false doesn't necessarily mean that the organization doesn't subscribe to Secrets Manager.
        // If there are changes to any subscription item, Stripe sends every item in the subscription, both
        // changed and unchanged.
        var previousSubscriptionHasSecretsManager =
            previousSubscription?.Items is not null &&
            previousSubscription.Items.Any(
                previousSubscriptionItem => planIdsOfPlansWithSecretManager.Contains(previousSubscriptionItem.Plan.Id));

        var currentSubscriptionHasSecretsManager =
            subscription.Items.Any(
                currentSubscriptionItem => planIdsOfPlansWithSecretManager.Contains(currentSubscriptionItem.Plan.Id));

        if (!previousSubscriptionHasSecretsManager || currentSubscriptionHasSecretsManager)
        {
            return;
        }

        var customerHasSecretsManagerTrial = subscription.Customer
            ?.Discount
            ?.Coupon
            ?.Id == "sm-standalone";

        var subscriptionHasSecretsManagerTrial = subscription.Discounts.Select(discount => discount.Coupon.Id)
            .Contains(CouponIDs.SecretsManagerStandalone);

        if (customerHasSecretsManagerTrial)
        {
            await _stripeAdapter.DeleteCustomerDiscountAsync(subscription.CustomerId);
        }

        if (subscriptionHasSecretsManagerTrial)
        {
            await _stripeAdapter.DeleteSubscriptionDiscountAsync(subscription.Id);
        }
    }

    private async Task HandleScheduleTriggeredFamiliesMigrationAsync(
        Event parsedEvent,
        Subscription subscription,
        Guid organizationId)
    {
        try
        {
            // Only act on schedule-managed subscriptions (schedule transitions set ScheduleId)
            if (subscription.ScheduleId == null)
            {
                return;
            }

            // Deserialize previous state to inspect which prices changed
            var previousSubscription = parsedEvent.Data.PreviousAttributes?.ToObject<Subscription>() as Subscription;
            if (previousSubscription?.Items?.Data == null)
            {
                return;
            }

            // Verify the subscription now carries the current Families price
            var familiesPlan = await _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually);

            if (!subscription.Items.Any(item => item.Price.Id == familiesPlan.PasswordManager.StripePlanId))
            {
                return;
            }

            // Verify the previous subscription had an old Families price — this distinguishes
            // a price migration from other schedule-triggered item changes (e.g., storage updates)
            var families2019Plan = await _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019);
            var families2025Plan = await _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2025);
            var migratingPriceIds = new HashSet<string>
            {
                families2019Plan.PasswordManager.StripePlanId,
                families2025Plan.PasswordManager.StripePlanId
            };

            if (!previousSubscription.Items.Data.Any(item =>
                    item.Price?.Id != null && migratingPriceIds.Contains(item.Price.Id)))
            {
                return;
            }

            // Sync org DB to match the plan Stripe already transitioned to
            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            if (organization == null)
            {
                _logger.LogWarning(
                    "Organization ({OrganizationId}) not found for schedule-triggered Families migration",
                    organizationId);
                return;
            }

            organization.PlanType = familiesPlan.Type;
            organization.Plan = familiesPlan.Name;
            organization.UsersGetPremium = familiesPlan.UsersGetPremium;
            organization.Seats = familiesPlan.PasswordManager.BaseSeats;

            await _organizationRepository.ReplaceAsync(organization);

            _logger.LogInformation(
                "Updated organization ({OrganizationId}) to FamiliesAnnually plan after schedule transition",
                organizationId);
        }
        catch (BillingException)
        {
            // GetPlanOrThrow calls throw BillingException when the pricing service returns
            // a non-success/non-404 status (e.g., 500/503 during an outage) or when the
            // response body fails to deserialize. Rethrowing lets the webhook return 500
            // so Stripe retries the event once the pricing service recovers.
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to handle schedule-triggered Families migration for organization ({OrganizationId})",
                organizationId);
        }
    }

    private async Task HandleScheduleTriggeredBusinessMigrationAsync(
        Event parsedEvent,
        Subscription subscription,
        Guid organizationId)
    {
        try
        {
            if (!_featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration))
            {
                return;
            }

            if (subscription.ScheduleId == null)
            {
                return;
            }

            var previousSubscription = parsedEvent.Data.PreviousAttributes?.ToObject<Subscription>() as Subscription;
            if (previousSubscription?.Items?.Data == null)
            {
                _logger.LogWarning(
                    "Schedule-triggered business migration fired for organization ({OrganizationId}) but the event had no previous subscription items to inspect",
                    organizationId);
                return;
            }

            var assignment = await _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId);
            if (assignment == null)
            {
                return;
            }

            if (assignment.MigratedDate.HasValue)
            {
                _logger.LogInformation(
                    "Schedule-triggered business migration already applied for organization ({OrganizationId}); skipping (assignment.MigratedDate is set)",
                    organizationId);
                return;
            }

            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            if (organization == null)
            {
                _logger.LogWarning(
                    "Organization ({OrganizationId}) not found for schedule-triggered business migration",
                    organizationId);
                return;
            }

            var cohort = await _cohortRepository.GetByIdAsync(assignment.CohortId);
            if (cohort == null || cohort.MigrationPathId == null)
            {
                _logger.LogWarning(
                    "Schedule-triggered business migration fired for organization ({OrganizationId}) but cohort ({CohortId}) is missing or has no MigrationPathId",
                    organizationId,
                    assignment.CohortId);
                return;
            }

            var migrationPath = MigrationPaths.FromId(cohort.MigrationPathId.Value);
            if (migrationPath == null)
            {
                _logger.LogWarning(
                    "Schedule-triggered business migration fired for organization ({OrganizationId}) but cohort ({CohortId}) references unregistered MigrationPathId ({MigrationPathId})",
                    organizationId,
                    cohort.Id,
                    cohort.MigrationPathId.Value);
                return;
            }

            var sourcePlan = await _pricingClient.GetPlanOrThrow(migrationPath.FromPlan);

            // A Packaged source (Teams Starter via HasNonSeatBased, Teams 2019 via ActualUsage) is identified
            // by its base price, which is present even when a sub-5 org has no seat-overage line; a Scalable
            // source by its per-seat price.
            var isPackagedSourcePlan = sourcePlan.IsPackagedMigrationSource(migrationPath.SeatCountPolicy);
            var sourcePriceId = isPackagedSourcePlan
                ? sourcePlan.PasswordManager.StripePlanId
                : sourcePlan.PasswordManager.StripeSeatPlanId;
            if (string.IsNullOrEmpty(sourcePriceId))
            {
                _logger.LogWarning(
                    "Schedule-triggered business migration for organization ({OrganizationId}): source plan ({SourcePlanType}) has no resolvable PasswordManager price id; skipping",
                    organizationId,
                    sourcePlan.Type);
                return;
            }

            if (!previousSubscription.Items.Data.Any(item =>
                    item.Price?.Id != null && item.Price.Id == sourcePriceId))
            {
                return;
            }

            var targetPlan = await _pricingClient.GetPlanOrThrow(migrationPath.ToPlan);
            var targetPriceId = GetPasswordManagerPriceId(targetPlan);
            if (string.IsNullOrEmpty(targetPriceId))
            {
                _logger.LogWarning(
                    "Schedule-triggered business migration for organization ({OrganizationId}): target plan ({TargetPlanType}) has no resolvable PasswordManager price id; skipping",
                    organizationId,
                    targetPlan.Type);
                return;
            }

            if (!subscription.Items.Any(item => item.Price?.Id != null && item.Price.Id == targetPriceId))
            {
                _logger.LogWarning(
                    "Schedule-triggered business migration for organization ({OrganizationId}): expected target price ({ExpectedPriceId}) for PlanType ({TargetPlanType}) not found in current subscription items; skipping",
                    organizationId,
                    targetPriceId,
                    targetPlan.Type);
                return;
            }

            organization.ChangePlan(targetPlan);

            // Packaged source plans (Teams Starter's flat bundle cap, Teams 2019's base seat allotment) store a
            // seat count in Seats that doesn't match the billed per-seat quantity; reconcile to what was billed.
            if (isPackagedSourcePlan)
            {
                var billedSeatQuantity = subscription.Items
                    .First(item => item.Price?.Id == targetPriceId).Quantity;
                organization.Seats = (int)Math.Max(1, billedSeatQuantity);
            }

            await _organizationRepository.ReplaceAsync(organization);

            var sourceProvidedServiceAccounts = sourcePlan.SecretsManager?.BaseServiceAccount ?? 0;
            var targetProvidedServiceAccounts = targetPlan.SecretsManager?.BaseServiceAccount ?? 0;
            var grace = Math.Max(0, sourceProvidedServiceAccounts - targetProvidedServiceAccounts);

            var sourceSecretsManagerSeatPlanId = sourcePlan.SecretsManager?.StripeSeatPlanId;
            var previousSubscriptionHasSecretsManager = sourceSecretsManagerSeatPlanId != null &&
                previousSubscription.Items.Data.Any(item => item.Price?.Id == sourceSecretsManagerSeatPlanId);

            if (grace > 0 && previousSubscriptionHasSecretsManager)
            {
                var metadata = new Dictionary<string, string>(subscription.Metadata)
                {
                    [MetadataKeys.MigrationGraceServiceAccounts] = grace.ToString(CultureInfo.InvariantCulture)
                };

                await _stripeAdapter.WaitForTestClockToAdvanceAsync(subscription.TestClock);

                try
                {
                    await _stripeAdapter.UpdateSubscriptionAsync(subscription.Id,
                        new SubscriptionUpdateOptions { Metadata = metadata });
                }
                catch (Exception graceException)
                {
                    // Surface as a BillingException so the generic catch below does not swallow it; the
                    // webhook returns 500 and Stripe retries. Because MigratedDate is not yet stamped,
                    // the replay re-runs this method: re-ChangePlan is a structural no-op and re-writing
                    // the same metadata key/value is idempotent.
                    _logger.LogError(
                        graceException,
                        "Business migration applied to organization ({OrganizationId}) but failed to write SM grace metadata; Stripe retry will re-apply",
                        organizationId);
                    throw new BillingException(
                        message: "Partial business migration write: organization updated but grace metadata write failed.",
                        innerException: graceException);
                }
            }

            try
            {
                assignment.MigratedDate = DateTime.UtcNow;
                assignment.RevisionDate = DateTime.UtcNow;
                await _cohortAssignmentRepository.ReplaceAsync(assignment);
            }
            catch (Exception assignmentException)
            {
                // Partial-write window: the organization was migrated successfully but the
                // assignment stamp failed. Log full cohort context so the inconsistent state
                // is observable, then surface as a BillingException. Stripe replays the same
                // event payload on retry and ChangePlan is structurally idempotent — re-applying
                // the same target plan shape is a no-op, and the assignment stamp re-runs cleanly.
                _logger.LogError(
                    assignmentException,
                    "Business migration applied to organization ({OrganizationId}) but failed to stamp MigratedDate on cohort assignment ({CohortId}, MigrationPathId {MigrationPathId}); Stripe retry will re-apply",
                    organizationId,
                    cohort.Id,
                    cohort.MigrationPathId.Value);
                throw new BillingException(
                    message: "Partial business migration write: organization updated but cohort assignment stamp failed.",
                    innerException: assignmentException);
            }

            _logger.LogInformation(
                "Schedule-triggered business migration applied for organization ({OrganizationId}): PlanType {SourcePlanType} -> {TargetPlanType}, cohort ({CohortId})",
                organizationId,
                migrationPath.FromPlan,
                migrationPath.ToPlan,
                cohort.Id);
        }
        catch (BillingException)
        {
            // Rethrow distinguishes a partial-write or pricing-service failure from a regular
            // catch-all; without this, the generic catch below would swallow it and Stripe
            // would not retry. GetPlanOrThrow surfaces BillingException for pricing-service
            // errors, unknown plan types, and malformed responses.
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to handle schedule-triggered business migration for organization ({OrganizationId})",
                organizationId);
        }
    }

    private static string GetPasswordManagerPriceId(Bit.Core.Models.StaticStore.Plan plan) =>
        plan.HasNonSeatBasedPasswordManagerPlan()
            ? plan.PasswordManager.StripePlanId
            : plan.PasswordManager.StripeSeatPlanId;
}
