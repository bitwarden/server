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

        var (cartLevelDiscounts, productLevelDiscounts) = GetStripeDiscounts(subscription);

        var (scheduleDiscounts, scheduleCouponIds) = cartLevelDiscounts.Count == 0
            ? await GetSchedulePhase2DiscountAsync(subscription)
            : (new List<BitwardenDiscount>(), new List<string>());

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
            estimatedTax = await EstimatePremiumTaxAsync(subscription, plans, availablePlan, scheduleCouponIds);
        }

        var passwordManagerSeats = new CartItem
        {
            TranslationKey = "premiumMembership",
            Quantity = passwordManagerSeatsItem.Quantity,
            Cost = seatCost,
            Discounts = GetCartItemDiscounts(productLevelDiscounts, passwordManagerSeatsItem, scheduleDiscounts)
        };

        var additionalStorage = additionalStorageItem != null
            ? new CartItem
            {
                TranslationKey = "additionalStorageGB",
                Quantity = additionalStorageItem.Quantity,
                Cost = GetCost(additionalStorageItem),
                Discounts = GetCartItemDiscounts(productLevelDiscounts, additionalStorageItem)
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
            Discounts = ToBitwardenDiscounts(cartLevelDiscounts),
            EstimatedTax = estimatedTax
        };
    }

    #region Utilities

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
                    Items = subscription.Items.Select(item =>
                    {
                        var isSeatItem = plans.Any(plan => plan.Seat.StripePriceId == item.Price.Id);

                        return new InvoiceSubscriptionDetailsItemOptions
                        {
                            Price = isSeatItem ? availablePlan.Seat.StripePriceId : item.Price.Id,
                            Quantity = item.Quantity
                        };
                    }).ToList()
                };

                if (couponIds is { Count: > 0 })
                {
                    options.Discounts = couponIds
                        .Select(id => new InvoiceDiscountOptions { Coupon = id })
                        .ToList();
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

    private static (List<Discount> CartLevel, List<Discount> ProductLevel) GetStripeDiscounts(
        Subscription subscription)
    {
        var discounts = new List<Discount>();

        if (subscription.Customer.Discount.IsValid())
        {
            discounts.Add(subscription.Customer.Discount);
        }

        discounts.AddRange(subscription.Discounts.Where(discount => discount.IsValid()));

        var cartLevel = new List<Discount>();
        var productLevel = new List<Discount>();

        foreach (var discount in discounts)
        {
            switch (discount)
            {
                // A null AppliesTo means "no product restrictions" — treat as cart-level (bug fix:
                // previously these fell through the switch and were silently dropped).
                case { Coupon.AppliesTo: null }:
                case { Coupon.AppliesTo.Products: null or { Count: 0 } }:
                    cartLevel.Add(discount);
                    break;
                case { Coupon.AppliesTo.Products.Count: > 0 }:
                    productLevel.Add(discount);
                    break;
            }
        }

        return (cartLevel, productLevel);
    }

    private async Task<(List<BitwardenDiscount> Discounts, List<string> CouponIds)> GetSchedulePhase2DiscountAsync(Subscription subscription)
    {
        if (string.IsNullOrEmpty(subscription.ScheduleId))
        {
            return ([], []);
        }

        try
        {
            var schedule = await stripeAdapter.GetSubscriptionScheduleAsync(subscription.ScheduleId,
                new SubscriptionScheduleGetOptions
                {
                    Expand = ["phases.discounts.coupon"]
                });

            if (schedule.Status != SubscriptionScheduleStatus.Active || schedule.Phases.Count < 2)
            {
                return ([], []);
            }

            var phase2 = schedule.Phases[1];
            var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

            if (phase2.StartDate < now)
            {
                logger.LogInformation(
                    "Schedule phase 2 for subscription schedule ({ScheduleID}) has already started, skipping discount display",
                    subscription.ScheduleId);
                return ([], []);
            }

            var discounts = ToBitwardenDiscounts(phase2.Discounts ?? [], d => d.Coupon);

            var couponIds = (phase2.Discounts ?? [])
                .Where(d => d.CouponId != null)
                .Select(d => d.CouponId!)
                .ToList();

            return (discounts, couponIds);
        }
        catch (StripeException stripeException)
        {
            logger.LogError(stripeException,
                "Failed to retrieve subscription schedule ({ScheduleID}) for discount resolution",
                subscription.ScheduleId);
            return ([], []);
        }
    }

    /// <summary>
    /// Resolves discounts for a cart item. Product-level discounts take precedence; schedule
    /// (Phase 2) discounts are only used as a fallback when no product-level discount applies.
    /// This mirrors the previous single-discount behavior (<c>productDiscount ?? scheduleDiscount</c>).
    /// </summary>
    private static List<BitwardenDiscount> GetCartItemDiscounts(
        List<Discount> productLevelDiscounts,
        SubscriptionItem subscriptionItem,
        List<BitwardenDiscount>? scheduleDiscounts = null)
    {
        var discounts = ToBitwardenDiscounts(productLevelDiscounts.Where(d => d.AppliesTo(subscriptionItem)));

        if (discounts.Count == 0 && scheduleDiscounts is { Count: > 0 })
        {
            discounts.AddRange(scheduleDiscounts);
        }

        return discounts;
    }

    /// <summary>
    /// Converts a sequence of Stripe objects to <see cref="BitwardenDiscount"/>s, filtering out
    /// any that don't produce a valid discount (e.g. invalid coupons or zero-value discounts).
    /// </summary>
    private static List<BitwardenDiscount> ToBitwardenDiscounts(IEnumerable<Discount> discounts) =>
        discounts
            .Select(d => (BitwardenDiscount?)d)
            .Where(d => d != null)
            .Cast<BitwardenDiscount>()
            .ToList();

    /// <inheritdoc cref="ToBitwardenDiscounts(IEnumerable{Discount})"/>
    private static List<BitwardenDiscount> ToBitwardenDiscounts<T>(
        IEnumerable<T> source,
        Func<T, Coupon?> couponSelector) =>
        source
            .Select(item => (BitwardenDiscount?)couponSelector(item))
            .Where(d => d != null)
            .Cast<BitwardenDiscount>()
            .ToList();

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

    #endregion
}
