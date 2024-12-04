using Bit.Core.Billing.Models;
using Bit.Core.Services;
using Stripe;

namespace Bit.Core.Billing;

public static class Utilities
{
    public const string BraintreeCustomerIdKey = "btCustomerId";
    public const string BraintreeCustomerIdOldKey = "btCustomerId_old";

    public static async Task<SubscriptionSuspension> GetSubscriptionSuspensionAsync(
        IStripeAdapter stripeAdapter,
        Subscription subscription)
    {
        if (subscription.Status is not "past_due" && subscription.Status is not "unpaid")
        {
            return null;
        }

        var openInvoices = await stripeAdapter.InvoiceSearchAsync(new InvoiceSearchOptions
        {
            Query = $"subscription:'{subscription.Id}' status:'open'"
        });

        if (openInvoices.Count == 0)
        {
            return null;
        }

        var currentDate = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

        switch (subscription.CollectionMethod)
        {
            case "charge_automatically":
                {
                    var firstOverdueInvoice = openInvoices
                        .Where(invoice => invoice.PeriodEnd < currentDate && invoice.Attempted)
                        .MinBy(invoice => invoice.Created);

                    if (firstOverdueInvoice == null)
                    {
                        return null;
                    }

                    const int gracePeriod = 14;

                    return new SubscriptionSuspension(
                        firstOverdueInvoice.Created.AddDays(gracePeriod),
                        firstOverdueInvoice.PeriodEnd,
                        gracePeriod);
                }
            case "send_invoice":
                {
                    var firstOverdueInvoice = openInvoices
                        .Where(invoice => invoice.DueDate < currentDate)
                        .MinBy(invoice => invoice.Created);

                    if (firstOverdueInvoice?.DueDate == null)
                    {
                        return null;
                    }

                    const int gracePeriod = 30;

                    return new SubscriptionSuspension(
                        firstOverdueInvoice.DueDate.Value.AddDays(gracePeriod),
                        firstOverdueInvoice.PeriodEnd,
                        gracePeriod);
                }
            default: return null;
        }
    }

    public static TaxInformation GetTaxInformation(Customer customer)
    {
        if (customer.Address == null)
        {
            return null;
        }

        return new TaxInformation(
            customer.Address.Country,
            customer.Address.PostalCode,
            customer.TaxIds?.FirstOrDefault()?.Value,
            customer.Address.Line1,
            customer.Address.Line2,
            customer.Address.City,
            customer.Address.State);
    }
}
