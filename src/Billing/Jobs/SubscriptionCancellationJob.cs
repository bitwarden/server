using Bit.Billing.Services;
using Bit.Core.Repositories;
using Quartz;
using Stripe;

namespace Bit.Billing.Jobs;

public class SubscriptionCancellationJob(
    IStripeFacade stripeFacade,
    IOrganizationRepository organizationRepository)
    : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var subscriptionId = context.MergedJobDataMap.GetString("subscriptionId");
        var organizationId = new Guid(context.MergedJobDataMap.GetString("organizationId") ?? string.Empty);

        var organization = await organizationRepository.GetByIdAsync(organizationId);
        if (organization == null || organization.Enabled)
        {
            // Organization was deleted or re-enabled by CS, skip cancellation
            return;
        }

        var subscription = await stripeFacade.GetSubscription(subscriptionId);
        if (subscription?.Status != "unpaid")
        {
            // Subscription is no longer unpaid, skip cancellation
            return;
        }

        // Cancel the subscription
        await stripeFacade.CancelSubscription(subscriptionId, new SubscriptionCancelOptions());

        // Void any open invoices
        var options = new InvoiceListOptions
        {
            Status = "open",
            Subscription = subscriptionId
        };
        var invoices = await stripeFacade.ListInvoices(options);
        foreach (var invoice in invoices)
        {
            await stripeFacade.VoidInvoice(invoice.Id);
        }
    }
}
