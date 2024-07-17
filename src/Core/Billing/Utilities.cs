using Bit.Core.Billing.Models;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Stripe;

namespace Bit.Core.Billing;

public static class Utilities
{
    public const string BraintreeCustomerIdKey = "btCustomerId";

    public static string BuildBraintreeCustomerId(ISubscriber subscriber) =>
        subscriber.BraintreeCustomerIdPrefix() +
        subscriber.Id.ToString("N").ToLower() +
        CoreHelpers.RandomString(3, upper: false, numeric: false);

    public static async Task<SubscriptionSuspensionDTO> GetSuspensionAsync(
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

                    return new SubscriptionSuspensionDTO(
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

                    return new SubscriptionSuspensionDTO(
                        firstOverdueInvoice.DueDate.Value.AddDays(gracePeriod),
                        firstOverdueInvoice.PeriodEnd,
                        gracePeriod);
                }
            default: return null;
        }
    }
}
