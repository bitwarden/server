using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Premium.Commands;

public interface IPreviewPremiumTaxCommand
{
    Task<BillingCommandResult<(decimal Tax, decimal Total)>> Run(
        int additionalStorage,
        BillingAddress billingAddress);
}

public class PreviewPremiumTaxCommand(
    ILogger<PreviewPremiumTaxCommand> logger,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter) : BaseBillingCommand<PreviewPremiumTaxCommand>(logger), IPreviewPremiumTaxCommand
{
    public Task<BillingCommandResult<(decimal Tax, decimal Total)>> Run(
        int additionalStorage,
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

            if (additionalStorage > 0)
            {
                options.SubscriptionDetails.Items.Add(new InvoiceSubscriptionDetailsItemOptions
                {
                    Price = premiumPlan.Storage.StripePriceId,
                    Quantity = additionalStorage
                });
            }

            var invoice = await stripeAdapter.InvoiceCreatePreviewAsync(options);
            return GetAmounts(invoice);
        });

    private static (decimal, decimal) GetAmounts(Invoice invoice) => (
        Convert.ToDecimal(invoice.Tax) / 100,
        Convert.ToDecimal(invoice.Total) / 100);
}
