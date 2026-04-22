using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Stripe;
using Stripe.TestHelpers;
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
        _logger = logger;
    }

    public async Task HandleAsync(Event parsedEvent)
    {
        var subscription = await _stripeEventService.GetSubscription(parsedEvent, true, ["customer", "discounts", "latest_invoice", "test_clock"]);
        SubscriberId subscriberId = subscription;

        var currentPeriodEnd = subscription.GetCurrentPeriodEnd();

        if (SubscriptionWentUnpaid(parsedEvent, subscription))
        {
            await DisableSubscriberAsync(subscriberId, currentPeriodEnd);
            await SetSubscriptionToCancelAsync(subscription);
        }
        else if (SubscriptionWentIncompleteExpired(parsedEvent, subscription))
        {
            // Subscription is already terminal in Stripe; any attempt to
            // schedule a cancel would be rejected and 500 the webhook,
            // causing Stripe to retry and re-run DisableSubscriberAsync.
            await DisableSubscriberAsync(subscriberId, currentPeriodEnd);
        }
        else if (SubscriptionBecameActive(parsedEvent, subscription))
        {
            await EnableSubscriberAsync(subscriberId, currentPeriodEnd);
            await RemovePendingCancellationAsync(subscription);
        }

        await subscriberId.Match(
            userId => _userService.UpdatePremiumExpirationAsync(userId.Value, currentPeriodEnd),
            async organizationId =>
            {
                if (_featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal))
                {
                    await HandleScheduleTriggeredFamiliesMigrationAsync(parsedEvent, subscription, organizationId.Value);
                }

                await _organizationService.UpdateExpirationDateAsync(organizationId.Value, currentPeriodEnd);

                if (_stripeEventUtilityService.IsSponsoredSubscription(subscription) && currentPeriodEnd.HasValue)
                {
                    await _organizationSponsorshipRenewCommand.UpdateExpirationDateAsync(organizationId.Value, currentPeriodEnd.Value);
                }

                await RemovePasswordManagerCouponIfRemovingSecretsManagerTrialAsync(parsedEvent, subscription);
            },
            _ => Task.CompletedTask);
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
            LatestInvoice.BillingReason: BillingReasons.SubscriptionCreate or BillingReasons.SubscriptionCycle
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

    private Task DisableSubscriberAsync(SubscriberId subscriberId, DateTime? currentPeriodEnd) =>
        subscriberId.Match(
            async userId =>
            {
                await _userService.DisablePremiumAsync(userId.Value, currentPeriodEnd);
                var user = await _userRepository.GetByIdAsync(userId.Value);
                if (user != null)
                {
                    await _pushNotificationAdapter.NotifyPremiumStatusChangedAsync(user);
                }
            },
            async organizationId =>
            {
                await _organizationDisableCommand.DisableAsync(organizationId.Value, currentPeriodEnd);
                var organization = await _organizationRepository.GetByIdAsync(organizationId.Value);
                if (organization != null)
                {
                    await _pushNotificationAdapter.NotifyEnabledChangedAsync(organization);
                }
            },
            async providerId =>
            {
                var provider = await _providerRepository.GetByIdAsync(providerId.Value);
                if (provider != null)
                {
                    provider.Enabled = false;
                    await _providerService.UpdateAsync(provider);
                }
            });

    private Task EnableSubscriberAsync(SubscriberId subscriberId, DateTime? currentPeriodEnd) =>
        subscriberId.Match(
            async userId =>
            {
                await _userService.EnablePremiumAsync(userId.Value, currentPeriodEnd);
                var user = await _userRepository.GetByIdAsync(userId.Value);
                if (user != null)
                {
                    await _pushNotificationAdapter.NotifyPremiumStatusChangedAsync(user!);
                }
            },
            async organizationId =>
            {
                await _organizationEnableCommand.EnableAsync(organizationId.Value, currentPeriodEnd);
                var organization = await _organizationRepository.GetByIdAsync(organizationId.Value);
                if (organization != null)
                {
                    await _pushNotificationAdapter.NotifyEnabledChangedAsync(organization);
                }
            },
            async providerId =>
            {
                var provider = await _providerRepository.GetByIdAsync(providerId.Value);
                if (provider != null)
                {
                    provider.Enabled = true;
                    await _providerService.UpdateAsync(provider);
                }
            });

    private async Task SetSubscriptionToCancelAsync(Subscription subscription)
    {
        await _priceIncreaseScheduler.Release(subscription.CustomerId, subscription.Id);

        if (subscription.TestClock != null)
        {
            await WaitForTestClockToAdvanceAsync(subscription.TestClock);
        }

        var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

        await _stripeAdapter.UpdateSubscriptionAsync(subscription.Id, new SubscriptionUpdateOptions
        {
            CancelAt = now.AddDays(7),
            ProrationBehavior = ProrationBehavior.None,
            CancellationDetails = new SubscriptionCancellationDetailsOptions
            {
                Comment = $"Automation: Setting unpaid subscription to cancel 7 days from {now:yyyy-MM-dd}."
            }
        });
    }

    private async Task RemovePendingCancellationAsync(Subscription subscription)
    {
        await _priceIncreaseScheduler.Schedule(subscription);

        await _stripeAdapter.UpdateSubscriptionAsync(subscription.Id, new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = false,
            ProrationBehavior = ProrationBehavior.None
        });
    }

    /// <summary>
    /// Removes the Password Manager coupon if the organization is removing the Secrets Manager trial.
    /// Only applies to organizations that have a subscription from the Secrets Manager trial.
    /// </summary>
    /// <param name="parsedEvent"></param>
    /// <param name="subscription"></param>
    private async Task RemovePasswordManagerCouponIfRemovingSecretsManagerTrialAsync(
        Event parsedEvent,
        Subscription subscription)
    {
        if (parsedEvent.Data.PreviousAttributes?.items is null)
        {
            return;
        }

        var organization = subscription.Metadata.TryGetValue("organizationId", out var organizationId)
            ? await _organizationRepository.GetByIdAsync(Guid.Parse(organizationId))
            : null;

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

    private async Task WaitForTestClockToAdvanceAsync(TestClock testClock)
    {
        while (testClock.Status != "ready")
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            testClock = await _stripeAdapter.GetTestClockAsync(testClock.Id);
            if (testClock.Status == "internal_failure")
            {
                throw new Exception("Stripe Test Clock encountered an internal failure");
            }
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
}
