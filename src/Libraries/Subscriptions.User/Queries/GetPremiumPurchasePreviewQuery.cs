using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.User.Models.Requests;
using Stripe;
using UserEntity = Bit.Core.Entities.User;

namespace Bit.Subscriptions.User.Queries;

internal interface IGetPremiumPurchasePreviewQuery
{
    Task<InvoicePreview> Run(UserEntity user, GetPremiumPurchasePreviewRequest request);
}

internal sealed class GetPremiumPurchasePreviewQuery(
    IPricingClient pricingClient,
    ISubscriptionDiscountService subscriptionDiscountService,
    IInvoicePreviewService invoicePreviewService) : IGetPremiumPurchasePreviewQuery
{
    public async Task<InvoicePreview> Run(UserEntity user, GetPremiumPurchasePreviewRequest request)
    {
        var additionalStorage = request.AdditionalStorage ?? 0;
        if (additionalStorage is < 0 or > 99)
        {
            throw new BadRequestException(
                nameof(GetPremiumPurchasePreviewRequest.AdditionalStorage), "Additional storage must be between 0 and 99 GB.");
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

        try
        {
            return await invoicePreviewService.GetInvoicePreviewAsync(options, PlanTierType.Premium, PlanCadenceType.Annually);
        }
        catch (StripeException stripeException)
            when (stripeException.StripeError?.Code == StripeConstants.ErrorCodes.CustomerTaxLocationInvalid)
        {
            throw new BadRequestException(
                "Your location wasn't recognized. Please ensure your country and postal code are valid and try again.");
        }
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
                nameof(GetPremiumPurchasePreviewRequest.Country), "Country code must be 2 characters long.");
        }

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            throw new BadRequestException(
                nameof(GetPremiumPurchasePreviewRequest.PostalCode), "The PostalCode field is required.");
        }

        return new AddressOptions { Country = country, PostalCode = postalCode };
    }
}
