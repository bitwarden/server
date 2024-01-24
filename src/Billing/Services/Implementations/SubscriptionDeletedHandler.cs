using Bit.Billing.Constants;
using Bit.Core.Services;
using Stripe;

namespace Bit.Billing.Services.Implementations;

public class SubscriptionDeletedHandler : IWebhookEventHandler
{
    private const string _premiumPlanId = "premium-annually";
    private const string _premiumPlanIdAppStore = "premium-annually-app";

    private readonly IOrganizationService _organizationService;
    private readonly IUserService _userService;
    private readonly IStripeEventService _stripeEventService;
    private readonly IWebhookUtility _webhookUtility;

    public SubscriptionDeletedHandler(IOrganizationService organizationService,
        IUserService userService,
        IStripeEventService stripeEventService,
        IWebhookUtility webhookUtility)
    {
        _organizationService = organizationService;
        _userService = userService;
        _stripeEventService = stripeEventService;
        _webhookUtility = webhookUtility;
    }

    public bool CanHandle(Event parsedEvent)
    {
        return parsedEvent.Type.Equals(HandledStripeWebhook.SubscriptionDeleted);
    }

    public async Task HandleAsync(Event parsedEvent)
    {
        if (parsedEvent.Type.Equals(HandledStripeWebhook.SubscriptionDeleted))
        {
            var subscription = await _stripeEventService.GetSubscription(parsedEvent, true);
            var ids = _webhookUtility.GetIdsFromMetaData(subscription.Metadata);
            var organizationId = ids.Item1 ?? Guid.Empty;
            var userId = ids.Item2 ?? Guid.Empty;
            var subCanceled = subscription.Status == StripeSubscriptionStatus.Canceled;
            var subUnpaid = subscription.Status == StripeSubscriptionStatus.Unpaid;
            var subIncompleteExpired = subscription.Status == StripeSubscriptionStatus.IncompleteExpired;

            if (subCanceled || subUnpaid || subIncompleteExpired)
            {
                if (organizationId != Guid.Empty)
                {
                    await _organizationService.DisableAsync(organizationId, subscription.CurrentPeriodEnd);
                }
                else if (userId != Guid.Empty)
                {
                    if (subUnpaid && subscription.Items.Any(i => i.Price.Id is _premiumPlanId or _premiumPlanIdAppStore))
                    {
                        await CancelSubscriptionAsync(subscription.Id);
                        await VoidOpenInvoicesAsync(subscription.Id);
                    }

                    var user = await _userService.GetUserByIdAsync(userId);
                    if (user.Premium)
                    {
                        await _userService.DisablePremiumAsync(userId, subscription.CurrentPeriodEnd);
                    }
                }
            }
        }
    }

    private static async Task CancelSubscriptionAsync(string subscriptionId)
    {
        await new SubscriptionService().CancelAsync(subscriptionId, new SubscriptionCancelOptions());
    }

    private static async Task VoidOpenInvoicesAsync(string subscriptionId)
    {
        var invoiceService = new InvoiceService();
        var options = new InvoiceListOptions
        {
            Status = StripeInvoiceStatus.Open,
            Subscription = subscriptionId
        };
        var invoices = invoiceService.List(options);
        foreach (var invoice in invoices)
        {
            await invoiceService.VoidInvoiceAsync(invoice.Id);
        }
    }
}



