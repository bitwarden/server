using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.User.Models.Requests;
using Microsoft.Extensions.Logging;
using Stripe;
using UserEntity = Bit.Core.Entities.User;

namespace Bit.Subscriptions.User.Queries;

internal interface IGetSubscriptionPurchasePreviewQuery
{
    Task<InvoicePreview> Run(UserEntity user, GetSubscriptionPurchasePreviewRequest request);
}

internal sealed class GetSubscriptionPurchasePreviewQuery(
    ILogger<GetSubscriptionPurchasePreviewQuery> logger,
    IPricingClient pricingClient,
    ISubscriptionDiscountService subscriptionDiscountService,
    IInvoicePreviewService invoicePreviewService) : IGetSubscriptionPurchasePreviewQuery
{
    public async Task<InvoicePreview> Run(UserEntity user, GetSubscriptionPurchasePreviewRequest request)
    {
        var additionalStorage = request.AdditionalStorage ?? 0;
        if (additionalStorage is < 0 or > 99)
        {
            throw new BadRequestException(
                nameof(GetSubscriptionPurchasePreviewRequest.AdditionalStorage), "Additional storage must be between 0 and 99 GB.");
        }

        var billingAddress = ResolveBillingAddress(request.Country, request.PostalCode);

        var premiumPlan = await pricingClient.GetAvailablePremiumPlan();

        var items = new List<InvoiceSubscriptionDetailsItemOptions>
        {
            new() { Price = premiumPlan.Seat.StripePriceId, Quantity = 1 }
        };

        if (additionalStorage > 0)
        {
            items.Add(new InvoiceSubscriptionDetailsItemOptions
            {
                Price = premiumPlan.Storage.StripePriceId,
                Quantity = additionalStorage
            });
        }

        var options = new InvoiceCreatePreviewOptions
        {
            AutomaticTax = new InvoiceAutomaticTaxOptions { Enabled = true },
            Currency = "usd",
            CustomerDetails = new InvoiceCustomerDetailsOptions { Address = billingAddress },
            SubscriptionDetails = new InvoiceSubscriptionDetailsOptions
            {
                BillingMode = new InvoiceSubscriptionDetailsBillingModeOptions { Type = StripeConstants.BillingMode.Classic },
                Items = items
            },
            Discounts = await ResolveEligibleDiscountsAsync(user, request.Coupons)
        };

        InvoicePreview preview;
        try
        {
            preview = await invoicePreviewService.GetInvoicePreviewAsync(options, PlanTierType.Premium, PlanCadenceType.Annually);
        }
        catch (StripeException stripeException)
            when (stripeException.StripeError?.Code == StripeConstants.ErrorCodes.CustomerTaxLocationInvalid)
        {
            throw new BadRequestException(
                "Your location wasn't recognized. Please ensure your country and postal code are valid and try again.");
        }

        return PurchasePreviewGuard.RequireSeats(preview, logger, user.Id, items.Select(item => item.Price));
    }

    // All-or-nothing: an ineligible coupon drops every coupon rather than failing the preview.
    private async Task<List<InvoiceDiscountOptions>?> ResolveEligibleDiscountsAsync(UserEntity user, string[]? coupons)
    {
        var trimmedCoupons = (coupons ?? [])
            .Where(coupon => !string.IsNullOrWhiteSpace(coupon))
            .Select(coupon => coupon.Trim())
            .ToArray();

        if (trimmedCoupons.Length == 0)
        {
            return null;
        }

        var allEligible = await subscriptionDiscountService.ValidateDiscountEligibilityForUserAsync(
            user, trimmedCoupons, DiscountTierType.Premium);

        return allEligible
            ? trimmedCoupons.Select(coupon => new InvoiceDiscountOptions { Coupon = coupon }).ToList()
            : null;
    }

    private static AddressOptions ResolveBillingAddress(string? country, string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(country) || country.Length != 2)
        {
            throw new BadRequestException(
                nameof(GetSubscriptionPurchasePreviewRequest.Country), "Country code must be 2 characters long.");
        }

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            throw new BadRequestException(
                nameof(GetSubscriptionPurchasePreviewRequest.PostalCode), "The PostalCode field is required.");
        }

        return new AddressOptions { Country = country, PostalCode = postalCode };
    }
}
