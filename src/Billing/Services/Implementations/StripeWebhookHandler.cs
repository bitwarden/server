using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;


public abstract class StripeWebhookHandler
{
    public const decimal PremiumPlanAppleIapPrice = 14.99M;
    public const string PremiumPlanId = "premium-annually";
    public const string PremiumPlanIdAppStore = "premium-annually-app";
    protected StripeWebhookHandler NextHandler { get; private set; }

    public void SetNextHandler(StripeWebhookHandler handler)
    {
        NextHandler = handler;
    }

    public async Task HandleRequest(Event parsedEvent)
    {
        if (CanHandle(parsedEvent))
        {
            await ProcessEvent(parsedEvent);
        }
        else if (NextHandler != null)
        {
            await NextHandler.HandleRequest(parsedEvent);
        }
    }

    protected abstract bool CanHandle(Event parsedEvent);
    protected abstract Task<IActionResult> ProcessEvent(Event parsedEvent);

    public static Tuple<Guid?, Guid?> GetIdsFromMetaData(IDictionary<string, string> metaData)
    {
        if (metaData == null || !metaData.Any())
        {
            return new Tuple<Guid?, Guid?>(null, null);
        }

        Guid? orgId = null;
        Guid? userId = null;

        if (metaData.ContainsKey("organizationId"))
        {
            orgId = new Guid(metaData["organizationId"]);
        }
        else if (metaData.ContainsKey("userId"))
        {
            userId = new Guid(metaData["userId"]);
        }

        if (userId == null && orgId == null)
        {
            var orgIdKey = metaData.Keys.FirstOrDefault(k => k.ToLowerInvariant() == "organizationid");
            if (!string.IsNullOrWhiteSpace(orgIdKey))
            {
                orgId = new Guid(metaData[orgIdKey]);
            }
            else
            {
                var userIdKey = metaData.Keys.FirstOrDefault(k => k.ToLowerInvariant() == "userid");
                if (!string.IsNullOrWhiteSpace(userIdKey))
                {
                    userId = new Guid(metaData[userIdKey]);
                }
            }
        }

        return new Tuple<Guid?, Guid?>(orgId, userId);
    }

    public static bool IsSponsoredSubscription(Subscription subscription) =>
        StaticStore.SponsoredPlans.Any(p => p.StripePlanId == subscription.Id);

    public static bool UnpaidAutoChargeInvoiceForSubscriptionCycle(Invoice invoice)
    {
        return invoice.AmountDue > 0 && !invoice.Paid && invoice.CollectionMethod == "charge_automatically" &&
               invoice.BillingReason == "subscription_cycle" && invoice.SubscriptionId != null;
    }

    public async Task<bool> AttemptToPayInvoiceAsync(Invoice invoice, bool attemptToPayWithStripe = false)
    {
        var customerService = new CustomerService();
        var customer = await customerService.GetAsync(invoice.CustomerId);
        if (customer?.Metadata?.ContainsKey("appleReceipt") ?? false)
        {
            return await AttemptToPayInvoiceWithAppleReceiptAsync(invoice, customer);
        }

        if (customer?.Metadata?.ContainsKey("btCustomerId") ?? false)
        {
            return await AttemptToPayInvoiceWithBraintreeAsync(invoice, customer);
        }

        if (attemptToPayWithStripe)
        {
            return await AttemptToPayInvoiceWithStripeAsync(invoice);
        }

        return false;
    }

    private async Task<bool> AttemptToPayInvoiceWithAppleReceiptAsync(Invoice invoice, Customer customer)
    {
        if (!customer?.Metadata?.ContainsKey("appleReceipt") ?? true)
        {
            return false;
        }

        var originalAppleReceiptTransactionId = customer.Metadata["appleReceipt"];
        var appleReceiptRecord = await _appleIapService.GetReceiptAsync(originalAppleReceiptTransactionId);
        if (string.IsNullOrWhiteSpace(appleReceiptRecord?.Item1) || !appleReceiptRecord.Item2.HasValue)
        {
            return false;
        }

        var subscriptionService = new SubscriptionService();
        var subscription = await subscriptionService.GetAsync(invoice.SubscriptionId);
        var ids = GetIdsFromMetaData(subscription?.Metadata);
        if (!ids.Item2.HasValue)
        {
            // Apple receipt is only for user subscriptions
            return false;
        }

        if (appleReceiptRecord.Item2.Value != ids.Item2.Value)
        {
            _logger.LogError("User Ids for Apple Receipt and subscription do not match: {0} != {1}.",
                appleReceiptRecord.Item2.Value, ids.Item2.Value);
            return false;
        }

        var appleReceiptStatus = await _appleIapService.GetVerifiedReceiptStatusAsync(appleReceiptRecord.Item1);
        if (appleReceiptStatus == null)
        {
            // TODO: cancel sub if receipt is cancelled?
            return false;
        }

        var receiptExpiration = appleReceiptStatus.GetLastExpiresDate().GetValueOrDefault(DateTime.MinValue);
        var invoiceDue = invoice.DueDate.GetValueOrDefault(DateTime.MinValue);
        if (receiptExpiration <= invoiceDue)
        {
            _logger.LogWarning("Apple receipt expiration is before invoice due date. {0} <= {1}",
                receiptExpiration, invoiceDue);
            return false;
        }

        var receiptLastTransactionId = appleReceiptStatus.GetLastTransactionId();
        var existingTransaction = await _transactionRepository.GetByGatewayIdAsync(
            GatewayType.AppStore, receiptLastTransactionId);
        if (existingTransaction != null)
        {
            _logger.LogWarning("There is already an existing transaction for this Apple receipt.",
                receiptLastTransactionId);
            return false;
        }

        var appleTransaction = appleReceiptStatus.BuildTransactionFromLastTransaction(
            PremiumPlanAppleIapPrice, ids.Item2.Value);
        appleTransaction.Type = TransactionType.Charge;

        var invoiceService = new InvoiceService();
        try
        {
            await invoiceService.UpdateAsync(invoice.Id, new InvoiceUpdateOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["appleReceipt"] = appleReceiptStatus.GetOriginalTransactionId(),
                    ["appleReceiptTransactionId"] = receiptLastTransactionId
                }
            });

            await _transactionRepository.CreateAsync(appleTransaction);
            await invoiceService.PayAsync(invoice.Id, new InvoicePayOptions { PaidOutOfBand = true });
        }
        catch (Exception e)
        {
            if (e.Message.Contains("Invoice is already paid"))
            {
                await invoiceService.UpdateAsync(invoice.Id, new InvoiceUpdateOptions
                {
                    Metadata = invoice.Metadata
                });
            }
            else
            {
                throw;
            }
        }

        return true;
    }

}
