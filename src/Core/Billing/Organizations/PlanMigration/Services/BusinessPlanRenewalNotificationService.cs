using System.Globalization;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Models.Mail.Billing.Renewal.BusinessPlanRenewal2020Migration;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;
using Stripe;

using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Billing.Organizations.PlanMigration.Services;

public class BusinessPlanRenewalNotificationService(
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter,
    IMailer mailer,
    IOrganizationRepository organizationRepository,
    ILogger<BusinessPlanRenewalNotificationService> logger)
    : IBusinessPlanRenewalNotificationService
{
    public async Task<bool> SendRenewalEmailAsync(
        Organization organization, Subscription subscription, OrganizationPlanMigrationCohort? cohort)
    {
        if (cohort?.MigrationPathId is null)
        {
            logger.LogWarning(
                "Cohort ({CohortId}) missing or has no MigrationPathId; skipping renewal email for Organization ({OrganizationId})",
                cohort?.Id, organization.Id);
            return false;
        }

        var migrationPath = MigrationPaths.FromId(cohort.MigrationPathId.Value);
        if (migrationPath is null)
        {
            logger.LogWarning(
                "Unknown MigrationPathId ({MigrationPathId}) on cohort ({CohortId}); skipping renewal email for Organization ({OrganizationId})",
                cohort.MigrationPathId, cohort.Id, organization.Id);
            return false;
        }

        var renewalDate = subscription.GetCurrentPeriodEnd();
        if (renewalDate is null)
        {
            // The migration is already committed by the time we reach here, so an indeterminate renewal date
            // leaves the organization with no renewal email; log at error severity so it reaches alerting.
            logger.LogError(
                "Business price migration was scheduled for Organization ({OrganizationId}), but the renewal date on subscription ({SubscriptionId}) was indeterminate, so no renewal email was sent; manual notification may be required",
                organization.Id, subscription.Id);
            return false;
        }

        var sourcePlan = await pricingClient.GetPlanOrThrow(migrationPath.FromPlan);
        var targetPlan = await pricingClient.GetPlanOrThrow(migrationPath.ToPlan);

        var culture = new CultureInfo("en-US");
        var seats = await ResolveSeatCountAsync(subscription, sourcePlan, organization, migrationPath.SeatCountPolicy);

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

        return true;
    }

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
                s.SubscriptionId == subscription.Id && s.Status == StripeConstants.SubscriptionScheduleStatus.Active);

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

    private sealed record Discount(bool IsPercentage, decimal Value, string Display, string CouponId, long Months);

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

    private static string FormatCurrency(decimal amount, CultureInfo culture) =>
        amount == decimal.Truncate(amount)
            ? amount.ToString("C0", culture)
            : amount.ToString("C2", culture);
}
