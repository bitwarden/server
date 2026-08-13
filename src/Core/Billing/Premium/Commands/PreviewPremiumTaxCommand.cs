using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Premium.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Premium.Commands;

public interface IPreviewPremiumTaxCommand
{
    Task<BillingCommandResult<(decimal Tax, decimal Total)>> Run(
        User user,
        PremiumPurchasePreview preview,
        BillingAddress billingAddress);
}

public class PreviewPremiumTaxCommand(
    ILogger<PreviewPremiumTaxCommand> logger,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter,
    ISubscriptionDiscountService subscriptionDiscountService) : BaseBillingCommand<PreviewPremiumTaxCommand>(logger), IPreviewPremiumTaxCommand
{
    public Task<BillingCommandResult<(decimal Tax, decimal Total)>> Run(
        User user,
        PremiumPurchasePreview preview,
        BillingAddress billingAddress)
        => HandleAsync<(decimal, decimal)>(async () =>
        {
            var premiumPlan = await pricingClient.GetAvailablePremiumPlan();

            var options = new InvoiceCreatePreviewOptions
            {
                AutomaticTax = new InvoiceAutomaticTaxOptions { Enabled = true },
                CustomerDetails = new InvoiceCustomerDetailsOptions
                {
                    Address = new AddressOptions
                    {
                        Country = billingAddress.Country,
                        PostalCode = billingAddress.PostalCode
                    }
                },
                Currency = "usd",
                SubscriptionDetails = new InvoiceSubscriptionDetailsOptions
                {
                    Items =
                    [
                        new InvoiceSubscriptionDetailsItemOptions { Price = premiumPlan.Seat.StripePriceId, Quantity = 1 }
                    ]
                }
            };

            if (preview.AdditionalStorageGb > 0)
            {
                options.SubscriptionDetails.Items.Add(new InvoiceSubscriptionDetailsItemOptions
                {
                    Price = premiumPlan.Storage.StripePriceId,
                    Quantity = preview.AdditionalStorageGb
                });
            }

            // Validate all coupons at once. If all are eligible, apply them; otherwise skip gracefully.
            if (preview.Coupons is { Length: > 0 })
            {
                var trimmedCoupons = preview.Coupons
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c.Trim())
                    .ToArray();

                if (trimmedCoupons.Length > 0)
                {
                    var allValid = await subscriptionDiscountService.ValidateDiscountEligibilityForUserAsync(
                        user, trimmedCoupons, DiscountTierType.Premium);

                    if (allValid)
                    {
                        options.Discounts = trimmedCoupons
                            .Select(c => new InvoiceDiscountOptions { Coupon = c })
                            .ToList();
                    }
                }
            }

            var invoice = await stripeAdapter.CreateInvoicePreviewAsync(options);
            return GetAmounts(invoice);
        });

    private static (decimal, decimal) GetAmounts(Invoice invoice) => (
        Convert.ToDecimal(invoice.TotalTaxes.Sum(invoiceTotalTax => invoiceTotalTax.Amount)) / 100,
        Convert.ToDecimal(invoice.Total) / 100);
}
