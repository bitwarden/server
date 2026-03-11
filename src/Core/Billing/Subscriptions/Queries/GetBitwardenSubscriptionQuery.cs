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

        var (cartLevelDiscount, productLevelDiscounts) = GetStripeDiscounts(subscription);

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
            estimatedTax = await EstimatePremiumTaxAsync(subscription, plans, availablePlan);
        }

        var passwordManagerSeats = new CartItem
        {
            TranslationKey = "premiumMembership",
            Quantity = passwordManagerSeatsItem.Quantity,
            Cost = seatCost,
            Discount = productLevelDiscounts.FirstOrDefault(discount => discount.AppliesTo(passwordManagerSeatsItem))
        };

        var additionalStorage = additionalStorageItem != null
            ? new CartItem
            {
                TranslationKey = "additionalStorageGB",
                Quantity = additionalStorageItem.Quantity,
                Cost = GetCost(additionalStorageItem),
                Discount = productLevelDiscounts.FirstOrDefault(discount => discount.AppliesTo(additionalStorageItem))
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
            Discount = cartLevelDiscount,
            EstimatedTax = estimatedTax
        };
    }

    #region Utilities

    private async Task<decimal> EstimatePremiumTaxAsync(
        Subscription subscription,
        List<PremiumPlan>? plans = null,
        PremiumPlan? availablePlan = null)
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

    private static (Discount? CartLevel, List<Discount> ProductLevel) GetStripeDiscounts(
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
                case { Coupon.AppliesTo.Products: null or { Count: 0 } }:
                    cartLevel.Add(discount);
                    break;
                case { Coupon.AppliesTo.Products.Count: > 0 }:
                    productLevel.Add(discount);
                    break;
            }
        }

        return (cartLevel.FirstOrDefault(), productLevel);
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

    #endregion
}
