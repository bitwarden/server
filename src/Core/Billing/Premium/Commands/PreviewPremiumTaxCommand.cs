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

            // Validate coupon and only apply if valid. If invalid, proceed without the discount.
            if (!string.IsNullOrWhiteSpace(preview.Coupon))
            {
                var isValid = await subscriptionDiscountService.ValidateDiscountForUserAsync(
                    user,
                    preview.Coupon.Trim(),
                    DiscountAudienceType.UserHasNoPreviousSubscriptions);

                if (isValid)
                {
                    options.Discounts = [new InvoiceDiscountOptions { Coupon = preview.Coupon.Trim() }];
                }
            }

            var invoice = await stripeAdapter.CreateInvoicePreviewAsync(options);
            return GetAmounts(invoice);
        });

    private static (decimal, decimal) GetAmounts(Invoice invoice) => (
        Convert.ToDecimal(invoice.TotalTaxes.Sum(invoiceTotalTax => invoiceTotalTax.Amount)) / 100,
        Convert.ToDecimal(invoice.Total) / 100);
}
