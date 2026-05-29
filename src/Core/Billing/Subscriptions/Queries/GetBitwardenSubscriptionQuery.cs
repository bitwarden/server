using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Microsoft.Extensions.Logging;
using OneOf;
using Stripe;

namespace Bit.Core.Billing.Subscriptions.Queries;

using static StripeConstants;
using static Utilities;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;

public interface IGetBitwardenSubscriptionQuery
{
    /// <summary>
    /// Retrieves detailed subscription information for a user, including subscription status,
    /// cart items, discounts, and billing details.
    /// </summary>
    /// <param name="user">The user whose subscription information to retrieve.</param>
    /// <returns>
    /// A <see cref="BitwardenSubscription"/> containing the subscription details, or null if no
    /// subscription is found or the subscription status is not recognized.
    /// </returns>
    /// <remarks>
    /// Currently only supports <see cref="User"/> subscribers. Future versions will support all
    /// <see cref="ISubscriber"/> types (User and Organization).
    /// </remarks>
    Task<BitwardenSubscription?> Run(User user);
}

public class GetBitwardenSubscriptionQuery(
    ILogger<GetBitwardenSubscriptionQuery> logger,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter) : IGetBitwardenSubscriptionQuery
{
    public async Task<BitwardenSubscription?> Run(User user)
    {
        if (string.IsNullOrEmpty(user.GatewaySubscriptionId))
        {
            return null;
        }

        var subscription = await FetchSubscriptionAsync(user);

        if (subscription == null)
        {
            return null;
        }

        var cart = await GetPremiumCartAsync(subscription);

        var baseSubscription = new BitwardenSubscription { Status = subscription.Status, Cart = cart, Storage = user };

        switch (subscription.Status)
        {
            case SubscriptionStatus.Incomplete:
            case SubscriptionStatus.IncompleteExpired:
                return baseSubscription with { Suspension = subscription.Created.AddHours(23), GracePeriod = 1 };

            case SubscriptionStatus.Trialing:
            case SubscriptionStatus.Active:
                return baseSubscription with
                {
                    NextCharge = subscription.GetCurrentPeriodEnd(),
                    CancelAt = subscription.CancelAt
                };

            case SubscriptionStatus.PastDue:
            case SubscriptionStatus.Unpaid:
                var suspension = await GetSubscriptionSuspensionAsync(stripeAdapter, subscription);
                if (suspension == null)
                {
                    return baseSubscription;
                }
                return baseSubscription with { Suspension = suspension.SuspensionDate, GracePeriod = suspension.GracePeriod };

            case SubscriptionStatus.Canceled:
                return baseSubscription with { Canceled = subscription.CanceledAt };

            default:
                {
                    logger.LogError("Subscription ({SubscriptionID}) has an unmanaged status ({Status})", subscription.Id, subscription.Status);
                    throw new ConflictException("Subscription is in an invalid state. Please contact support for assistance.");
                }
        }
    }

    private async Task<Cart> GetPremiumCartAsync(
        Subscription subscription)
    {
        var plans = await pricingClient.ListPremiumPlans();

        var passwordManagerSeatsItem = subscription.Items.FirstOrDefault(item =>
            plans.Any(plan => plan.Seat.StripePriceId == item.Price.Id));

        if (passwordManagerSeatsItem == null)
        {
            throw new ConflictException("Premium subscription does not have a Password Manager line item.");
        }

        var additionalStorageItem = subscription.Items.FirstOrDefault(item =>
            plans.Any(plan => plan.Storage.StripePriceId == item.Price.Id));

        var coupons = await GetRelevantCouponsAsync(subscription);
        var (cartLevelCoupon, productLevelCoupons) = PartitionCouponsByScope(coupons);

        var availablePlan = plans.First(plan => plan.Available);
        var onCurrentPricing = passwordManagerSeatsItem.Price.Id == availablePlan.Seat.StripePriceId;

        decimal seatCost;
        decimal estimatedTax;

        if (onCurrentPricing)
        {
            seatCost = GetCost(passwordManagerSeatsItem);
            estimatedTax = await EstimatePremiumTaxAsync(subscription);
        }
        else
        {
            seatCost = availablePlan.Seat.Price;
            estimatedTax = await EstimatePremiumTaxAsync(
                subscription, plans, availablePlan,
                [.. coupons.Select(c => c.Id)]);
        }

        var passwordManagerSeats = new CartItem
        {
            TranslationKey = "premiumMembership",
            Quantity = passwordManagerSeatsItem.Quantity,
            Cost = seatCost,
            Discount = productLevelCoupons.FirstOrDefault(coupon => coupon.AppliesTo(passwordManagerSeatsItem))
        };

        var additionalStorage = additionalStorageItem != null
            ? new CartItem
            {
                TranslationKey = "additionalStorageGB",
                Quantity = additionalStorageItem.Quantity,
                Cost = GetCost(additionalStorageItem),
                Discount = productLevelCoupons.FirstOrDefault(coupon => coupon.AppliesTo(additionalStorageItem))
            }
            : null;

        return new Cart
        {
            PasswordManager = new PasswordManagerCartItems
            {
                Seats = passwordManagerSeats,
                AdditionalStorage = additionalStorage
            },
            Cadence = PlanCadenceType.Annually,
            Discount = cartLevelCoupon,
            EstimatedTax = estimatedTax
        };
    }

    private async Task<decimal> EstimatePremiumTaxAsync(
        Subscription subscription,
        List<PremiumPlan>? plans = null,
        PremiumPlan? availablePlan = null,
        List<string>? couponIds = null)
    {
        try
        {
            var options = new InvoiceCreatePreviewOptions
            {
                Customer = subscription.Customer.Id
            };

            if (plans != null && availablePlan != null)
            {
                options.AutomaticTax = new InvoiceAutomaticTaxOptions
                {
                    Enabled = subscription.AutomaticTax?.Enabled ?? false
                };

                options.SubscriptionDetails = new InvoiceSubscriptionDetailsOptions
                {
                    Items = [.. subscription.Items.Select(item =>
                    {
                        var isSeatItem = plans.Any(plan => plan.Seat.StripePriceId == item.Price.Id);

                        return new InvoiceSubscriptionDetailsItemOptions
                        {
                            Price = isSeatItem ? availablePlan.Seat.StripePriceId : item.Price.Id,
                            Quantity = item.Quantity
                        };
                    })]
                };

                if (couponIds is { Count: > 0 })
                {
                    options.Discounts = [.. couponIds.Select(id => new InvoiceDiscountOptions { Coupon = id })];
                }
            }
            else
            {
                options.Subscription = subscription.Id;
            }

            var invoice = await stripeAdapter.CreateInvoicePreviewAsync(options);

            return GetCost(invoice.TotalTaxes);
        }
        catch (StripeException stripeException) when
            (stripeException.StripeError.Code == ErrorCodes.InvoiceUpcomingNone)
        {
            return 0;
        }
    }

    private static decimal GetCost(OneOf<SubscriptionItem, List<InvoiceTotalTax>> value) =>
        value.Match(
            item => (item.Price.UnitAmountDecimal ?? 0) / 100M,
            taxes => taxes.Sum(invoiceTotalTax => invoiceTotalTax.Amount) / 100M);

    /// <summary>
    /// Returns the coupons relevant to the subscription's upcoming invoice. When a subscription
    /// schedule is attached, Phase 2's discounts are the source of truth (they reflect the
    /// upcoming-renewal state, including any preserved current discounts plus migration coupons).
    /// Otherwise the subscription's current discounts are used. Customer-level discounts apply
    /// independently of the schedule and are always included.
    /// </summary>
    private async Task<List<Coupon>> GetRelevantCouponsAsync(Subscription subscription)
    {
        var coupons = new List<Coupon>();

        if (subscription.Customer.Discount.IsValid())
        {
            coupons.Add(subscription.Customer.Discount.Coupon);
        }

        if (!string.IsNullOrEmpty(subscription.ScheduleId))
        {
            coupons.AddRange(await GetSchedulePhase2CouponsAsync(subscription));
        }
        else
        {
            coupons.AddRange((subscription.Discounts ?? [])
                .Where(d => d.IsValid())
                .Select(d => d.Coupon));
        }

        return coupons;
    }

    private static (Coupon? CartLevel, List<Coupon> ProductLevel) PartitionCouponsByScope(
        IEnumerable<Coupon> coupons)
    {
        var cartLevel = new List<Coupon>();
        var productLevel = new List<Coupon>();

        foreach (var coupon in coupons)
        {
            switch (coupon)
            {
                case { AppliesTo.Products: null or { Count: 0 } }:
                case { AppliesTo: null }:
                    cartLevel.Add(coupon);
                    break;
                case { AppliesTo.Products.Count: > 0 }:
                    productLevel.Add(coupon);
                    break;
            }
        }

        return (cartLevel.FirstOrDefault(), productLevel);
    }

    private async Task<List<Coupon>> GetSchedulePhase2CouponsAsync(Subscription subscription)
    {
        try
        {
            var schedule = await stripeAdapter.GetSubscriptionScheduleAsync(subscription.ScheduleId,
                new SubscriptionScheduleGetOptions
                {
                    Expand = ["phases.discounts.coupon.applies_to"]
                });

            if (schedule.Status != SubscriptionScheduleStatus.Active || schedule.Phases.Count < 2)
            {
                return [];
            }

            var phase2 = schedule.Phases[1];
            var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

            if (phase2.StartDate < now)
            {
                logger.LogInformation(
                    "Schedule phase 2 for subscription schedule ({ScheduleID}) has already started, skipping discount display",
                    subscription.ScheduleId);
                return [];
            }

            return phase2.Discounts?
                .Where(d => d?.Coupon?.Valid == true)
                .Select(d => d.Coupon)
                .ToList() ?? [];
        }
        catch (StripeException stripeException)
        {
            // Rethrow rather than soft-fail. The schedule's coupons feed both the discount display
            // and the tax-preview's `options.Discounts` list — silently dropping them would inflate
            // the tax estimate the user sees against the new pricing without any error signal.
            logger.LogError(stripeException,
                "Failed to retrieve subscription schedule ({ScheduleID}) for discount resolution",
                subscription.ScheduleId);
            throw;
        }
    }

    private async Task<Subscription?> FetchSubscriptionAsync(User user)
    {
        try
        {
            return await stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, new SubscriptionGetOptions
            {
                Expand =
                [
                    "customer.discount.coupon.applies_to",
                    "discounts.coupon.applies_to",
                    "items.data.price.product",
                    "test_clock"
                ]
            });
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Code == ErrorCodes.ResourceMissing)
        {
            logger.LogError("Subscription ({SubscriptionID}) for User ({UserID}) was not found", user.GatewaySubscriptionId, user.Id);
            return null;
        }
    }
}
