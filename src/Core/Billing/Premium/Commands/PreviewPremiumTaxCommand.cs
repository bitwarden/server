using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Services;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Premium.Commands;

using static StripeConstants;

public interface IPreviewPremiumTaxCommand
{
    Task<BillingCommandResult<(decimal Tax, decimal Total)>> Run(
        int additionalStorage,
        BillingAddress billingAddress);
}

public class PreviewPremiumTaxCommand(
    ILogger<PreviewPremiumTaxCommand> logger,
    IStripeAdapter stripeAdapter) : BaseBillingCommand<PreviewPremiumTaxCommand>(logger), IPreviewPremiumTaxCommand
{
    public Task<BillingCommandResult<(decimal Tax, decimal Total)>> Run(
        int additionalStorage,
        BillingAddress billingAddress)
        => HandleAsync<(decimal, decimal)>(async () =>
        {
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
                        new InvoiceSubscriptionDetailsItemOptions { Price = Prices.PremiumAnnually, Quantity = 1 }
                    ]
                }
            };

            if (additionalStorage > 0)
            {
                options.SubscriptionDetails.Items.Add(new InvoiceSubscriptionDetailsItemOptions
                {
                    Price = Prices.StoragePlanPersonal,
                    Quantity = additionalStorage
                });
            }

            var invoice = await stripeAdapter.CreateInvoicePreviewAsync(options);
            return GetAmounts(invoice);
        });

    private static (decimal, decimal) GetAmounts(Invoice invoice) => (
        Convert.ToDecimal(invoice.TotalTaxes.Sum(invoiceTotalTax => invoiceTotalTax.Amount)) / 100,
        Convert.ToDecimal(invoice.Total) / 100);
}
