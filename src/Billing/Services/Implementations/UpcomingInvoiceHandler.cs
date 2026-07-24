using System.Globalization;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Organizations.PlanMigration;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Models.Mail.Billing.Renewal.BusinessPlanRenewal2020Migration;
using Bit.Core.Models.Mail.Billing.Renewal.Families2019Renewal;
using Bit.Core.Models.Mail.Billing.Renewal.Families2020Renewal;
using Bit.Core.Models.Mail.Billing.Renewal.Premium;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Stripe;
using Event = Stripe.Event;
using Plan = Bit.Core.Models.StaticStore.Plan;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;

namespace Bit.Billing.Services.Implementations;

using static StripeConstants;

public class UpcomingInvoiceHandler(
    IGetPaymentMethodQuery getPaymentMethodQuery,
    ILogger<StripeEventProcessor> logger,
    IMailService mailService,
    IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
    IOrganizationPlanMigrationCohortRepository cohortRepository,
    IOrganizationRepository organizationRepository,
    IPricingClient pricingClient,
    IProviderRepository providerRepository,
    IStripeAdapter stripeAdapter,
    IPriceIncreaseScheduler priceIncreaseScheduler,
    IStripeEventService stripeEventService,
    IStripeEventUtilityService stripeEventUtilityService,
    IUserRepository userRepository,
    IValidateSponsorshipCommand validateSponsorshipCommand,
    IMailer mailer,
    IFeatureService featureService)
    : IUpcomingInvoiceHandler
{
    public async Task HandleAsync(Event parsedEvent)
    {
        var invoice = await stripeEventService.GetInvoice(parsedEvent);

        var customer =
            await stripeAdapter.GetCustomerAsync(invoice.CustomerId,
                new CustomerGetOptions
                {
                    Expand =
                    [
                        "subscriptions",
                        "subscriptions.data.customer",
                        "subscriptions.data.discounts.coupon",
                        "subscriptions.data.test_clock",
                        "tax",
                        "tax_ids"
                    ]
                });

        var subscription = customer.Subscriptions.FirstOrDefault();

        if (subscription == null)
        {
            return;
        }

        var (organizationId, userId, providerId) = stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata);

        if (organizationId.HasValue)
        {
            await HandleOrganizationUpcomingInvoiceAsync(
                organizationId.Value,
                parsedEvent,
                invoice,
                customer,
                subscription);
        }
        else if (userId.HasValue)
        {
            await HandlePremiumUsersUpcomingInvoiceAsync(
                userId.Value,
                parsedEvent,
                invoice,
                customer,
                subscription);
        }
        else if (providerId.HasValue)
        {
            await HandleProviderUpcomingInvoiceAsync(
                providerId.Value,
                parsedEvent,
                invoice,
                customer,
                subscription);
        }
    }

    #region Organizations

    private async Task HandleOrganizationUpcomingInvoiceAsync(
        Guid organizationId,
        Event @event,
        Invoice invoice,
        Customer customer,
        Subscription subscription)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            logger.LogWarning("Could not find Organization ({OrganizationID}) for '{EventType}' event ({EventID})",
                organizationId, @event.Type, @event.Id);
            return;
        }

        await AlignOrganizationTaxConcernsAsync(organization, subscription, customer, @event.Id);

        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        var subscriptionAligned = await AlignOrganizationSubscriptionConcernsAsync(
            organization,
            @event,
            subscription,
            plan);

        /*
         * Subscription alignment sends out a different version of our Upcoming Invoice email, so we don't need to continue
         * with processing.
         */
        if (subscriptionAligned)
        {
            return;
        }

        // Don't send the upcoming invoice email unless the organization's on an annual plan.
        if (!plan.IsAnnual)
        {
            return;
        }

        if (stripeEventUtilityService.IsSponsoredSubscription(subscription))
        {
            var sponsorshipIsValid =
                await validateSponsorshipCommand.ValidateSponsorshipAsync(organizationId);

            if (!sponsorshipIsValid)
            {
                /*
                 * If the sponsorship is invalid, then the subscription was updated to use the regular families plan
                 * price. Given that this is the case, we need the new invoice amount
                 */
                invoice = await stripeAdapter.GetInvoiceAsync(subscription.LatestInvoiceId);
            }
        }

        await SendUpcomingInvoiceEmailsAsync([organization.BillingEmail], invoice);
    }

    private async Task AlignOrganizationTaxConcernsAsync(
        Organization organization,
        Subscription subscription,
        Customer customer,
        string eventId)
    {
        if (!subscription.AutomaticTax.Enabled)
        {
            try
            {
                await EnableAutomaticTaxAsync(subscription);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to set organization's ({OrganizationID}) subscription to automatic tax while processing event with ID {EventID}",
                    organization.Id,
                    eventId);
            }
        }
    }

    /// <summary>
    /// Dispatches subscription-alignment work based on the organization's product tier.
    /// </summary>
    /// <returns>
    /// True if a tier-specific alignment took ownership of this organization's renewal communication,
    /// so the caller must skip the standard upcoming-invoice email. The tier-specific path may still
    /// decide not to send (or fail to send) its own cohort-specific email — for example when the cohort
    /// or migration path is missing, the renewal date is indeterminate, or the email send fails after the
    /// migration was already scheduled — so True does not guarantee an email was sent. False if no
    /// alignment ran, in which case the caller falls through to the standard upcoming-invoice email path.
    /// </returns>
    private Task<bool> AlignOrganizationSubscriptionConcernsAsync(
        Organization organization,
        Event @event,
        Subscription subscription,
        Plan plan) =>
        organization.PlanType.GetProductTier() switch
        {
            ProductTierType.Families =>
                ScheduleFamiliesPriceMigrationAsync(organization, @event, subscription, plan),
            ProductTierType.Teams or ProductTierType.Enterprise or ProductTierType.TeamsStarter =>
                ScheduleBusinessPlanPriceMigrationAsync(organization, @event, subscription),
            _ => Task.FromResult(false)
        };

    private async Task<bool> ScheduleFamiliesPriceMigrationAsync(
        Organization organization,
        Event @event,
        Subscription subscription,
        Plan plan)
    {
        if (plan.Type is not (PlanType.FamiliesAnnually2019 or PlanType.FamiliesAnnually2025))
        {
            return false;
        }

        var passwordManagerItem =
            subscription.Items.FirstOrDefault(item => item.Price.Id == plan.PasswordManager.StripePlanId);

        if (passwordManagerItem == null)
        {
            logger.LogWarning("Could not find Organization's ({OrganizationId}) password manager item while processing '{EventType}' event ({EventID})",
                organization.Id, @event.Type, @event.Id);
            return false;
        }

        var familiesPlan = await pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually);

        try
        {
            if (featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal))
            {
                var scheduled = await priceIncreaseScheduler.SchedulePersonalPriceIncrease(subscription);
                if (!scheduled)
                {
                    return true;
                }
            }
            else
            {
                organization.PlanType = familiesPlan.Type;
                organization.Plan = familiesPlan.Name;
                organization.UsersGetPremium = familiesPlan.UsersGetPremium;
                organization.Seats = familiesPlan.PasswordManager.BaseSeats;

                var options = new SubscriptionUpdateOptions
                {
                    Items =
                    [
                        new SubscriptionItemOptions
                        {
                            Id = passwordManagerItem.Id,
                            Price = familiesPlan.PasswordManager.StripePlanId
                        }
                    ],
                    ProrationBehavior = ProrationBehavior.None
                };

                if (plan.Type == PlanType.FamiliesAnnually2019)
                {
                    options.Discounts =
                    [
                        new SubscriptionDiscountOptions { Coupon = CouponIDs.Milestone3SubscriptionDiscount }
                    ];

                    var premiumAccessAddOnItem = subscription.Items.FirstOrDefault(item =>
                        item.Price.Id == plan.PasswordManager.StripePremiumAccessPlanId);

                    if (premiumAccessAddOnItem != null)
                    {
                        options.Items.Add(new SubscriptionItemOptions
                        {
                            Id = premiumAccessAddOnItem.Id,
                            Deleted = true
                        });
                    }

                    var seatAddOnItem = subscription.Items.FirstOrDefault(item => item.Price.Id == "personal-org-seat-annually");

                    if (seatAddOnItem != null)
                    {
                        options.Items.Add(new SubscriptionItemOptions
                        {
                            Id = seatAddOnItem.Id,
                            Deleted = true
                        });
                    }
                }

                await organizationRepository.ReplaceAsync(organization);
                await stripeAdapter.UpdateSubscriptionAsync(subscription.Id, options);
            }

            await SendFamiliesRenewalEmailAsync(organization, familiesPlan, plan);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to align subscription concerns for Organization ({OrganizationID}) while processing '{EventType}' event ({EventID})",
                organization.Id,
                @event.Type,
                @event.Id);
            return false;
        }
    }

    private async Task<bool> ScheduleBusinessPlanPriceMigrationAsync(
        Organization organization,
        Event @event,
        Subscription subscription)
    {
        Guid cohortId;
        try
        {
            if (!featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration))
            {
                return false;
            }

            var assignment = await assignmentRepository.GetByOrganizationIdAsync(organization.Id);
            if (assignment is null || assignment.ScheduledDate is not null)
            {
                return false;
            }

            await stripeAdapter.WaitForTestClockToAdvanceAsync(subscription.TestClock);

            var migrationScheduled = await priceIncreaseScheduler.ScheduleForSubscription(subscription);

            if (!migrationScheduled)
            {
                return false;
            }

            cohortId = assignment.CohortId;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to schedule business price migration for Organization ({OrganizationID}) while processing '{EventType}' event ({EventID})",
                organization.Id,
                @event.Type,
                @event.Id);
            return false;
        }

        /*
         * The price migration is now committed at Stripe. From here on we must return true so the caller
         * suppresses the standard upcoming-invoice email (which would quote the pre-migration price). A
         * failure building or sending the cohort renewal email is logged but must not flip us back into the
         * standard-email path, otherwise a migrated organization would receive the wrong email.
         */
        try
        {
            var cohort = await cohortRepository.GetByIdAsync(cohortId);
            if (cohort?.MigrationPathId is null)
            {
                logger.LogWarning(
                    "Cohort ({CohortId}) missing or has no MigrationPathId; skipping renewal email for Organization ({OrganizationId})",
                    cohortId, organization.Id);
                return true;
            }

            var migrationPath = MigrationPaths.FromId(cohort.MigrationPathId.Value);
            if (migrationPath is null)
            {
                logger.LogWarning(
                    "Unknown MigrationPathId ({MigrationPathId}) on cohort ({CohortId}); skipping renewal email for Organization ({OrganizationId})",
                    cohort.MigrationPathId, cohort.Id, organization.Id);
                return true;
            }

            var sourcePlan = await pricingClient.GetPlanOrThrow(migrationPath.FromPlan);
            var targetPlan = await pricingClient.GetPlanOrThrow(migrationPath.ToPlan);

            await SendBusinessRenewalEmailAsync(
                organization, subscription, sourcePlan, targetPlan, cohort, migrationPath.SeatCountPolicy);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Business price migration was scheduled for Organization ({OrganizationID}), but the renewal notification email failed while processing '{EventType}' event ({EventID}); manual notification may be required",
                organization.Id,
                @event.Type,
                @event.Id);
        }

        return true;
    }

    private async Task SendBusinessRenewalEmailAsync(
        Organization organization,
        Subscription subscription,
        Plan sourcePlan,
        Plan targetPlan,
        OrganizationPlanMigrationCohort cohort,
        SeatCountPolicy seatCountPolicy)
    {
        var renewalDate = subscription.GetCurrentPeriodEnd();
        if (renewalDate is null)
        {
            // The migration is already committed by the time we reach here, so an indeterminate renewal date
            // leaves the organization with no renewal email and the standard email suppressed — the same
            // outcome as a post-schedule send failure. Log at the same severity so it reaches alerting.
            logger.LogError(
                "Business price migration was scheduled for Organization ({OrganizationId}), but the renewal date on subscription ({SubscriptionId}) was indeterminate, so no renewal email was sent; manual notification may be required",
                organization.Id, subscription.Id);
            return;
        }

        var culture = new CultureInfo("en-US");
        var seats = await ResolveSeatCountAsync(subscription, sourcePlan, organization, seatCountPolicy);

        // SeatPrice is a per-year figure on annual plans and a per-month figure on monthly plans. The per-user
        // monthly line always shows a monthly rate (annual ÷ 12); the recurring total is quoted in the plan's own
        // billing period — per year for annual cohorts, per month for monthly cohorts.
        var seatPrice = targetPlan.PasswordManager.SeatPrice;
        var perUserMonthly = targetPlan.IsAnnual ? seatPrice / 12 : seatPrice;
        var total = seatPrice * seats;

        var discounts = await ResolveDiscountsAsync(cohort, subscription, organization, culture);

        foreach (var discount in discounts.Where(discount => discount.IsPercentage))
        {
            total = total * (100 - discount.Value) / 100;
        }

        var totalAmountOff = discounts.Where(discount => !discount.IsPercentage).Sum(discount => discount.Value);
        if (total - totalAmountOff < 0)
        {
            // Discounts drove the quote below zero (e.g. a fixed-amount coupon larger than the discounted seat
            // total). We clamp to $0 for display, but a $0 renewal quote is anomalous and worth surfacing.
            logger.LogWarning(
                "Renewal email total for Organization ({OrganizationId}) went below zero after discounts and was clamped to 0",
                organization.Id);
        }
        total = Math.Max(0, total - totalAmountOff);

        await mailer.SendEmail(new BusinessPlanRenewal2020MigrationMail
        {
            ToEmails = [organization.BillingEmail],
            View = new BusinessPlanRenewal2020MigrationMailView
            {
                RenewalDate = renewalDate.Value.ToString("MMMM d, yyyy", culture),
                Seats = seats,
                PerUserMonthlyPrice = FormatCurrency(perUserMonthly, culture),
                IsAnnual = targetPlan.IsAnnual,
                TotalPrice = FormatCurrency(total, culture),
                DiscountLines = [.. discounts.Select(discount => discount.Display)],
                ProactiveDiscountMonths = discounts
                    .FirstOrDefault(discount => discount.CouponId == cohort.ProactiveDiscountCouponCode)?.Months ?? 0
            }
        });
    }

    private static string FormatCurrency(decimal amount, CultureInfo culture) =>
        amount == decimal.Truncate(amount)
            ? amount.ToString("C0", culture)
            : amount.ToString("C2", culture);

    private async Task<int> ResolveSeatCountAsync(
        Subscription subscription, Plan sourcePlan, Organization organization, SeatCountPolicy seatCountPolicy)
    {
        // A Packaged source's line items don't reflect the true seat total, so resolve the quote from
        // actual usage to match what the scheduler bills.
        if (sourcePlan.IsPackagedMigrationSource(seatCountPolicy))
        {
            var occupied = (await organizationRepository
                .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)).Total;
            var passwordManagerSeats = sourcePlan.ResolveMigratedSeatCount(occupied, organization.Seats);

            // Floor the quote on the Stripe SM seat line so the email matches the billed PM seats (SM <= PM).
            var secretsManagerSeatQuantity = subscription.Items.Data
                .FirstOrDefault(item =>
                    sourcePlan.SecretsManager is not null &&
                    item.Price?.Id == sourcePlan.SecretsManager.StripeSeatPlanId)?.Quantity ?? 0;

            return (int)Math.Max((long)passwordManagerSeats, secretsManagerSeatQuantity);
        }

        var seatItem = subscription.Items.Data
            .FirstOrDefault(item => item.Price?.Id == sourcePlan.PasswordManager.StripeSeatPlanId);

        var seats = seatItem?.Quantity ?? organization.Seats;
        if (seats is null)
        {
            // Neither the subscription's seat line item nor the organization had a seat count. This should not
            // happen for a paid business organization; surface it because the renewal email would otherwise
            // quote 0 seats and a $0.00 total.
            logger.LogWarning(
                "Could not resolve a seat count for Organization ({OrganizationId}) subscription ({SubscriptionId}); defaulting to 0 for the renewal email",
                organization.Id, subscription.Id);
        }

        return (int)(seats ?? 0);
    }

    private sealed record Discount(bool IsPercentage, decimal Value, string Display, string CouponId, long Months);

    private async Task<List<Discount>> ResolveDiscountsAsync(
        OrganizationPlanMigrationCohort cohort,
        Subscription subscription,
        Organization organization,
        CultureInfo culture)
    {
        var discounts = new List<Discount>();
        var seenCouponIds = new HashSet<string>();

        Discount? MapCoupon(Coupon? coupon, string couponId)
        {
            var months = coupon?.DurationInMonths ?? 0;

            if (coupon?.PercentOff is { } percentOff)
            {
                return new Discount(IsPercentage: true, Value: percentOff, Display: $"{percentOff}%",
                    CouponId: couponId, Months: months);
            }

            if (coupon?.AmountOff is { } amountOffMinorUnits)
            {
                var amountOff = amountOffMinorUnits / 100M;
                return new Discount(IsPercentage: false, Value: amountOff, Display: FormatCurrency(amountOff, culture),
                    CouponId: couponId, Months: months);
            }

            logger.LogError(
                "Discount coupon ({CouponId}) for Organization ({OrganizationId}) resolved with neither PercentOff nor AmountOff; it will not be reflected in the renewal email",
                couponId, organization.Id);
            return null;
        }

        void AddIfNew(Coupon? coupon, string couponId)
        {
            if (string.IsNullOrEmpty(couponId) || !seenCouponIds.Add(couponId))
            {
                return;
            }

            var discount = MapCoupon(coupon, couponId);
            if (discount is not null)
            {
                discounts.Add(discount);
            }
        }

        async Task ResolveAndAddAsync(Coupon? expandedCoupon, string? couponId)
        {
            if (string.IsNullOrEmpty(couponId) || seenCouponIds.Contains(couponId))
            {
                return;
            }

            if (expandedCoupon is not null)
            {
                AddIfNew(expandedCoupon, couponId);
                return;
            }

            try
            {
                var coupon = await stripeAdapter.GetCouponAsync(couponId);
                AddIfNew(coupon, couponId);
            }
            catch (StripeException exception)
            {
                logger.LogError(
                    exception,
                    "Could not retrieve discount coupon ({CouponId}) for Organization ({OrganizationId}); the renewal email will not reflect it | Code = {Code}",
                    couponId, organization.Id, exception.StripeError?.Code);
            }
        }

        await ResolveAndAddAsync(expandedCoupon: null, cohort.ProactiveDiscountCouponCode);

        foreach (var discount in subscription.Discounts ?? [])
        {
            if (discount is null)
            {
                logger.LogError(
                    "Subscription ({SubscriptionId}) for Organization ({OrganizationId}) returned a null discount entry; 'discounts.coupon' is likely no longer expanded and the renewal email may omit a discount",
                    subscription.Id, organization.Id);
                continue;
            }

            if (discount.Coupon is not { } coupon)
            {
                logger.LogError(
                    "Subscription ({SubscriptionId}) discount ({DiscountId}) for Organization ({OrganizationId}) has no expanded Coupon; 'discounts.coupon' is likely no longer expanded and the renewal email may omit a discount",
                    subscription.Id, discount.Id, organization.Id);
                continue;
            }

            AddIfNew(coupon, coupon.Id);
        }

        try
        {
            var schedules = await stripeAdapter.ListSubscriptionSchedulesAsync(
                new SubscriptionScheduleListOptions { Customer = subscription.CustomerId });

            var activeSchedule = schedules?.Data?.FirstOrDefault(s =>
                s.SubscriptionId == subscription.Id && s.Status == SubscriptionScheduleStatus.Active);

            if (activeSchedule != null)
            {
                var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;
                var migrationPhases = activeSchedule.Phases?.Where(phase => phase.EndDate > now).ToList() ?? [];

                SubscriptionSchedulePhase? postRenewalPhase = null;
                switch (migrationPhases.Count)
                {
                    case 2:
                        postRenewalPhase = migrationPhases[1];
                        break;
                    default:
                        logger.LogWarning(
                            "Schedule ({ScheduleId}) for Organization ({OrganizationId}) has {PhaseCount} unexpired phase(s); expected 2, so post-renewal discounts were not read for the renewal email",
                            activeSchedule.Id, organization.Id, migrationPhases.Count);
                        break;
                }

                foreach (var phaseDiscount in postRenewalPhase?.Discounts ?? [])
                {
                    await ResolveAndAddAsync(phaseDiscount?.Coupon, phaseDiscount?.CouponId);
                }
            }
        }
        catch (StripeException exception)
        {
            logger.LogError(
                exception,
                "Could not list subscription schedules for Organization ({OrganizationId}) subscription ({SubscriptionId}); the renewal email may not reflect schedule-phase discounts | Code = {Code}",
                organization.Id, subscription.Id, exception.StripeError?.Code);
        }

        return discounts;
    }

    #endregion

    #region Premium Users

    private async Task HandlePremiumUsersUpcomingInvoiceAsync(
        Guid userId,
        Event @event,
        Invoice invoice,
        Customer customer,
        Subscription subscription)
    {
        var user = await userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            logger.LogWarning("Could not find User ({UserID}) for '{EventType}' event ({EventID})",
                userId, @event.Type, @event.Id);
            return;
        }

        await AlignPremiumUsersTaxConcernsAsync(user, @event, customer, subscription);

        var subscriptionAligned = await AlignPremiumUsersSubscriptionConcernsAsync(user, @event, subscription);

        /*
         * Subscription alignment sends out a different version of our Upcoming Invoice email, so we don't need to continue
         * with processing.
         */
        if (subscriptionAligned)
        {
            return;
        }

        if (user.Premium)
        {
            await SendUpcomingInvoiceEmailsAsync(new List<string> { user.Email }, invoice);
        }
    }

    private async Task AlignPremiumUsersTaxConcernsAsync(
        User user,
        Event @event,
        Customer customer,
        Subscription subscription)
    {
        if (!subscription.AutomaticTax.Enabled && customer.HasRecognizedTaxLocation())
        {
            try
            {
                await EnableAutomaticTaxAsync(subscription);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to set user's ({UserID}) subscription to automatic tax while processing event with ID {EventID}",
                    user.Id,
                    @event.Id);
            }
        }
    }

    private async Task<bool> AlignPremiumUsersSubscriptionConcernsAsync(
        User user,
        Event @event,
        Subscription subscription)
    {
        var premiumPlans = await pricingClient.ListPremiumPlans();
        var oldPlan = premiumPlans.FirstOrDefault(p => !p.Available);
        var newPlan = premiumPlans.FirstOrDefault(p => p.Available);

        if (oldPlan == null || newPlan == null)
        {
            logger.LogWarning("Could not resolve old and new premium plans while processing '{EventType}' event ({EventID})",
                @event.Type, @event.Id);
            return false;
        }

        var premiumItem = subscription.Items.FirstOrDefault(i => i.Price.Id == oldPlan.Seat.StripePriceId);

        if (premiumItem == null)
        {
            logger.LogWarning("Could not find User's ({UserID}) premium subscription item while processing '{EventType}' event ({EventID})",
                user.Id, @event.Type, @event.Id);
            return false;
        }

        try
        {
            if (featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal))
            {
                var scheduled = await priceIncreaseScheduler.SchedulePersonalPriceIncrease(subscription);
                if (!scheduled)
                {
                    return true;
                }
            }
            else
            {
                await stripeAdapter.UpdateSubscriptionAsync(subscription.Id,
                    new SubscriptionUpdateOptions
                    {
                        Items =
                        [
                            new SubscriptionItemOptions { Id = premiumItem.Id, Price = newPlan.Seat.StripePriceId }
                        ],
                        Discounts =
                        [
                            new SubscriptionDiscountOptions { Coupon = CouponIDs.Milestone2SubscriptionDiscount }
                        ],
                        ProrationBehavior = ProrationBehavior.None
                    });
            }

            await SendPremiumRenewalEmailAsync(user, newPlan);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to update user's ({UserID}) subscription price id while processing event with ID {EventID}",
                user.Id,
                @event.Id);
            return false;
        }
    }

    #endregion

    #region Providers

    private async Task HandleProviderUpcomingInvoiceAsync(
        Guid providerId,
        Event @event,
        Invoice invoice,
        Customer customer,
        Subscription subscription)
    {
        var provider = await providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            logger.LogWarning("Could not find Provider ({ProviderID}) for '{EventType}' event ({EventID})",
                providerId, @event.Type, @event.Id);
            return;
        }

        await AlignProviderTaxConcernsAsync(provider, subscription, customer, @event.Id);

        if (!string.IsNullOrEmpty(provider.BillingEmail))
        {
            await SendProviderUpcomingInvoiceEmailsAsync(new List<string> { provider.BillingEmail }, invoice, subscription, providerId);
        }
    }

    private async Task AlignProviderTaxConcernsAsync(
        Provider provider,
        Subscription subscription,
        Customer customer,
        string eventId)
    {
        if (!subscription.AutomaticTax.Enabled)
        {
            try
            {
                await stripeAdapter.UpdateSubscriptionAsync(subscription.Id,
                    new SubscriptionUpdateOptions
                    {
                        AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true }
                    });
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to set provider's ({ProviderID}) subscription to automatic tax while processing event with ID {EventID}",
                    provider.Id,
                    eventId);
            }
        }
    }

    private async Task SendProviderUpcomingInvoiceEmailsAsync(IEnumerable<string> emails, Invoice invoice,
        Subscription subscription, Guid providerId)
    {
        var validEmails = emails.Where(e => !string.IsNullOrEmpty(e));

        var items = invoice.FormatForProvider(subscription);

        if (invoice.NextPaymentAttempt.HasValue && invoice.AmountDue > 0)
        {
            var provider = await providerRepository.GetByIdAsync(providerId);
            if (provider == null)
            {
                logger.LogWarning("Provider {ProviderId} not found for invoice upcoming email", providerId);
                return;
            }

            var collectionMethod = subscription.CollectionMethod;
            var paymentMethod = await getPaymentMethodQuery.Run(provider);

            var hasPaymentMethod = paymentMethod != null;
            var paymentMethodDescription = paymentMethod?.Match(
                bankAccount => $"Bank account ending in {bankAccount.Last4}",
                card => $"{card.Brand} ending in {card.Last4}",
                payPal => $"PayPal account {payPal.Email}"
            );

            await mailService.SendProviderInvoiceUpcoming(
                validEmails,
                invoice.AmountDue / 100M,
                invoice.NextPaymentAttempt.Value,
                items,
                collectionMethod,
                hasPaymentMethod,
                paymentMethodDescription);
        }
    }

    #endregion

    #region Shared

    private async Task EnableAutomaticTaxAsync(Subscription subscription)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal))
        {
            var schedules = await stripeAdapter.ListSubscriptionSchedulesAsync(
                new SubscriptionScheduleListOptions { Customer = subscription.CustomerId });

            var activeSchedule = schedules.Data.FirstOrDefault(s =>
                s.SubscriptionId == subscription.Id && s.Status == SubscriptionScheduleStatus.Active);

            if (activeSchedule != null)
            {
                var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;
                var phases = new List<SubscriptionSchedulePhaseOptions>();

                for (var i = 0; i < activeSchedule.Phases.Count; i++)
                {
                    var phase = activeSchedule.Phases[i];

                    // Skip phases that have already completed
                    if (phase.EndDate <= now)
                    {
                        continue;
                    }

                    // When a phase's predecessor has ended, the phase is already active and
                    // its one-time migration discount has been applied and consumed.
                    // Re-including it would cause Stripe to re-apply it.
                    var discountConsumed = i > 0 && activeSchedule.Phases[i - 1].EndDate <= now;

                    // Gate on StartDate > now, not !discountConsumed (false for the active phase 0),
                    // so we never re-stack the customer coupon onto the already-billing current period.
                    var customerDiscount = phase.StartDate > now ? subscription.Customer?.Discount : null;

                    phases.Add(new SubscriptionSchedulePhaseOptions
                    {
                        StartDate = phase.StartDate,
                        EndDate = phase.EndDate,
                        Items = phase.Items.Select(item => new SubscriptionSchedulePhaseItemOptions
                        {
                            Price = item.PriceId,
                            Quantity = item.Quantity
                        }).ToList(),
                        Discounts = discountConsumed
                            ? []
                            : customerDiscount.MergeDiscountCouponIds(
                                phase.Discounts?.Select(d => d.CouponId)).ToPhaseDiscountOptions(),
                        ProrationBehavior = phase.ProrationBehavior,
                        AutomaticTax = new SubscriptionSchedulePhaseAutomaticTaxOptions
                        {
                            Enabled = true
                        }
                    });
                }

                await stripeAdapter.UpdateSubscriptionScheduleAsync(activeSchedule.Id,
                    new SubscriptionScheduleUpdateOptions
                    {
                        DefaultSettings = new SubscriptionScheduleDefaultSettingsOptions
                        {
                            AutomaticTax = new SubscriptionScheduleDefaultSettingsAutomaticTaxOptions
                            {
                                Enabled = true
                            }
                        },
                        Phases = phases
                    });
                return;
            }
        }

        await stripeAdapter.UpdateSubscriptionAsync(subscription.Id,
            new SubscriptionUpdateOptions
            {
                AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true }
            });
    }

    private async Task SendUpcomingInvoiceEmailsAsync(IEnumerable<string> emails, Invoice invoice)
    {
        var validEmails = emails.Where(e => !string.IsNullOrEmpty(e));

        var items = invoice.Lines.Select(i => i.Description).ToList();

        if (invoice is { NextPaymentAttempt: not null, AmountDue: > 0 })
        {
            await mailService.SendInvoiceUpcoming(
                validEmails,
                invoice.AmountDue / 100M,
                invoice.NextPaymentAttempt.Value,
                items,
                true);
        }
    }

    private async Task SendFamiliesRenewalEmailAsync(
        Organization organization,
        Plan familiesPlan,
        Plan planBeforeAlignment)
    {
        await (planBeforeAlignment switch
        {
            { Type: PlanType.FamiliesAnnually2025 } => SendFamilies2020RenewalEmailAsync(organization, familiesPlan),
            { Type: PlanType.FamiliesAnnually2019 } => SendFamilies2019RenewalEmailAsync(organization, familiesPlan),
            _ => throw new InvalidOperationException("Unsupported families plan in SendFamiliesRenewalEmailAsync().")
        });
    }

    private async Task SendFamilies2020RenewalEmailAsync(Organization organization, Plan familiesPlan)
    {
        var email = new Families2020RenewalMail
        {
            ToEmails = [organization.BillingEmail],
            View = new Families2020RenewalMailView
            {
                MonthlyRenewalPrice = (familiesPlan.PasswordManager.BasePrice / 12).ToString("C", new CultureInfo("en-US"))
            }
        };

        await mailer.SendEmail(email);
    }

    private async Task SendFamilies2019RenewalEmailAsync(Organization organization, Plan familiesPlan)
    {
        var coupon = await stripeAdapter.GetCouponAsync(CouponIDs.Milestone3SubscriptionDiscount);
        if (coupon == null)
        {
            throw new InvalidOperationException($"Coupon for sending families 2019 email id:{CouponIDs.Milestone3SubscriptionDiscount} not found");
        }

        if (coupon.PercentOff == null)
        {
            throw new InvalidOperationException($"coupon.PercentOff for sending families 2019 email id:{CouponIDs.Milestone3SubscriptionDiscount} is null");
        }

        var discountedAnnualRenewalPrice = familiesPlan.PasswordManager.BasePrice * (100 - coupon.PercentOff.Value) / 100;

        var email = new Families2019RenewalMail
        {
            ToEmails = [organization.BillingEmail],
            View = new Families2019RenewalMailView
            {
                BaseMonthlyRenewalPrice = (familiesPlan.PasswordManager.BasePrice / 12).ToString("C", new CultureInfo("en-US")),
                BaseAnnualRenewalPrice = familiesPlan.PasswordManager.BasePrice.ToString("C", new CultureInfo("en-US")),
                DiscountAmount = $"{coupon.PercentOff}%",
                DiscountedAnnualRenewalPrice = discountedAnnualRenewalPrice.ToString("C", new CultureInfo("en-US"))
            }
        };

        await mailer.SendEmail(email);
    }

    private async Task SendPremiumRenewalEmailAsync(
        User user,
        PremiumPlan premiumPlan)
    {
        var coupon = await stripeAdapter.GetCouponAsync(CouponIDs.Milestone2SubscriptionDiscount);
        if (coupon == null)
        {
            throw new InvalidOperationException($"Coupon for sending premium renewal email id:{CouponIDs.Milestone2SubscriptionDiscount} not found");
        }

        if (coupon.PercentOff == null)
        {
            throw new InvalidOperationException($"coupon.PercentOff for sending premium renewal email id:{CouponIDs.Milestone2SubscriptionDiscount} is null");
        }

        var discountedAnnualRenewalPrice = premiumPlan.Seat.Price * (100 - coupon.PercentOff.Value) / 100;

        var email = new PremiumRenewalMail
        {
            ToEmails = [user.Email],
            View = new PremiumRenewalMailView
            {
                BaseMonthlyRenewalPrice = (premiumPlan.Seat.Price / 12).ToString("C", new CultureInfo("en-US")),
                DiscountAmount = $"{coupon.PercentOff}%",
                DiscountedAnnualRenewalPrice = discountedAnnualRenewalPrice.ToString("C", new CultureInfo("en-US"))
            }
        };

        await mailer.SendEmail(email);
    }

    #endregion
}
