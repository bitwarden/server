using Bit.Billing.Services;
using Bit.Core.Billing.Constants;
using Bit.Core.Repositories;
using Quartz;
using Stripe;

namespace Bit.Billing.Jobs;

using static StripeConstants;

public class SubscriptionCancellationJob(
    IStripeFacade stripeFacade,
    IOrganizationRepository organizationRepository,
    ILogger<SubscriptionCancellationJob> logger)
    : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var subscriptionId = context.MergedJobDataMap.GetString("subscriptionId");
        var organizationId = new Guid(context.MergedJobDataMap.GetString("organizationId") ?? string.Empty);

        var organization = await organizationRepository.GetByIdAsync(organizationId);
        if (organization == null || organization.Enabled)
        {
            logger.LogWarning("{Job} skipped for subscription ({SubscriptionID}) because organization is either null or enabled", nameof(SubscriptionCancellationJob), subscriptionId);
            // Organization was deleted or re-enabled by CS, skip cancellation
            return;
        }

        var subscription = await stripeFacade.GetSubscription(subscriptionId, new SubscriptionGetOptions
        {
            Expand = ["latest_invoice"]
        });

        if (subscription is not
            {
                Status: SubscriptionStatus.Unpaid,
                LatestInvoice.BillingReason: "subscription_cycle" or "subscription_create"
            })
        {
            logger.LogWarning("{Job} skipped for subscription ({SubscriptionID}) because subscription is not unpaid or does not have a cancellable billing reason", nameof(SubscriptionCancellationJob), subscriptionId);
            return;
        }

        // Cancel the subscription
        await stripeFacade.CancelSubscription(subscriptionId, new SubscriptionCancelOptions());

        logger.LogInformation("{Job} cancelled subscription ({SubscriptionID})", nameof(SubscriptionCancellationJob), subscriptionId);

        // Void any open invoices
        var options = new InvoiceListOptions
        {
            Status = "open",
            Subscription = subscriptionId,
            Limit = 100
        };
        var invoices = await stripeFacade.ListInvoices(options);
        foreach (var invoice in invoices)
        {
            await stripeFacade.VoidInvoice(invoice.Id);
            logger.LogInformation("{Job} voided invoice ({InvoiceID}) for subscription ({SubscriptionID})", nameof(SubscriptionCancellationJob), invoice.Id, subscriptionId);
        }

        while (invoices.HasMore)
        {
            options.StartingAfter = invoices.Data.Last().Id;
            invoices = await stripeFacade.ListInvoices(options);
            foreach (var invoice in invoices)
            {
                await stripeFacade.VoidInvoice(invoice.Id);
                logger.LogInformation("{Job} voided invoice ({InvoiceID}) for subscription ({SubscriptionID})", nameof(SubscriptionCancellationJob), invoice.Id, subscriptionId);
            }
        }
    }
}
