using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Braintree;
using Braintree.Exceptions;
using Stripe;
using Customer = Stripe.Customer;
using Subscription = Stripe.Subscription;
using TransactionType = Bit.Core.Enums.TransactionType;

namespace Bit.Billing.Services.Implementations;

public class WebhookUtility : IWebhookUtility
{
    private const decimal _premiumPlanAppleIapPrice = 14.99M;

    private readonly ILogger<WebhookUtility> _logger;
    private readonly IAppleIapService _appleIapService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly GlobalSettings _globalSettings;
    private readonly BraintreeGateway _btGateway;
    private readonly IMailService _mailService;

    public WebhookUtility(ILogger<WebhookUtility> logger,
        IAppleIapService appleIapService,
        ITransactionRepository transactionRepository,
        GlobalSettings globalSettings,
        IMailService mailService)
    {
        _logger = logger;
        _appleIapService = appleIapService;
        _transactionRepository = transactionRepository;
        _globalSettings = globalSettings;
        _btGateway = new BraintreeGateway
        {
            Environment = globalSettings.Braintree.Production ?
                Braintree.Environment.PRODUCTION : Braintree.Environment.SANDBOX,
            MerchantId = globalSettings.Braintree.MerchantId,
            PublicKey = globalSettings.Braintree.PublicKey,
            PrivateKey = globalSettings.Braintree.PrivateKey
        };
        _mailService = mailService;
    }

    public async Task<bool> AttemptToPayInvoice(Invoice invoice, bool attemptToPayWithStripe = false)
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

    public Tuple<Guid?, Guid?> GetIdsFromMetaData(IDictionary<string, string> metaData)
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

    public bool IsSponsoredSubscription(Subscription subscription) =>
        StaticStore.SponsoredPlans.Any(p => p.StripePlanId == subscription.Id);

    public bool UnpaidAutoChargeInvoiceForSubscriptionCycle(Invoice invoice)
    {
        return invoice.AmountDue > 0 && !invoice.Paid && invoice.CollectionMethod == "charge_automatically" &&
               invoice.BillingReason == "subscription_cycle" && invoice.SubscriptionId != null;
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
            _premiumPlanAppleIapPrice, ids.Item2.Value);
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

    private async Task<bool> AttemptToPayInvoiceWithBraintreeAsync(Invoice invoice, Customer customer)
    {
        _logger.LogDebug("Attempting to pay invoice with Braintree");
        if (!customer?.Metadata?.ContainsKey("btCustomerId") ?? true)
        {
            _logger.LogWarning(
                "Attempted to pay invoice with Braintree but btCustomerId wasn't on Stripe customer metadata");
            return false;
        }

        var subscriptionService = new SubscriptionService();
        var subscription = await subscriptionService.GetAsync(invoice.SubscriptionId);
        var ids = GetIdsFromMetaData(subscription?.Metadata);
        if (!ids.Item1.HasValue && !ids.Item2.HasValue)
        {
            _logger.LogWarning(
                "Attempted to pay invoice with Braintree but Stripe subscription metadata didn't contain either a organizationId or userId");
            return false;
        }

        var orgTransaction = ids.Item1.HasValue;
        var btObjIdField = orgTransaction ? "organization_id" : "user_id";
        var btObjId = ids.Item1 ?? ids.Item2.Value;
        var btInvoiceAmount = (invoice.AmountDue / 100M);

        var existingTransactions = orgTransaction ?
            await _transactionRepository.GetManyByOrganizationIdAsync(ids.Item1.Value) :
            await _transactionRepository.GetManyByUserIdAsync(ids.Item2.Value);
        var duplicateTimeSpan = TimeSpan.FromHours(24);
        var now = DateTime.UtcNow;
        var duplicateTransaction = existingTransactions?
            .FirstOrDefault(t => (now - t.CreationDate) < duplicateTimeSpan);
        if (duplicateTransaction != null)
        {
            _logger.LogWarning("There is already a recent PayPal transaction ({0}). " +
                "Do not charge again to prevent possible duplicate.", duplicateTransaction.GatewayId);
            return false;
        }

        Result<Braintree.Transaction> transactionResult;
        try
        {
            transactionResult = await _btGateway.Transaction.SaleAsync(
                new Braintree.TransactionRequest
                {
                    Amount = btInvoiceAmount,
                    CustomerId = customer.Metadata["btCustomerId"],
                    Options = new Braintree.TransactionOptionsRequest
                    {
                        SubmitForSettlement = true,
                        PayPal = new Braintree.TransactionOptionsPayPalRequest
                        {
                            CustomField =
                                $"{btObjIdField}:{btObjId},region:{_globalSettings.BaseServiceUri.CloudRegion}"
                        }
                    },
                    CustomFields = new Dictionary<string, string>
                    {
                        [btObjIdField] = btObjId.ToString(),
                        ["region"] = _globalSettings.BaseServiceUri.CloudRegion
                    }
                });
        }
        catch (NotFoundException e)
        {
            _logger.LogError(e,
                "Attempted to make a payment with Braintree, but customer did not exist for the given btCustomerId present on the Stripe metadata");
            throw;
        }

        if (!transactionResult.IsSuccess())
        {
            if (invoice.AttemptCount < 4)
            {
                await _mailService.SendPaymentFailedAsync(customer.Email, btInvoiceAmount, true);
            }
            return false;
        }

        var invoiceService = new InvoiceService();
        try
        {
            await invoiceService.UpdateAsync(invoice.Id, new InvoiceUpdateOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["btTransactionId"] = transactionResult.Target.Id,
                    ["btPayPalTransactionId"] =
                        transactionResult.Target.PayPalDetails?.AuthorizationId
                }
            });
            await invoiceService.PayAsync(invoice.Id, new InvoicePayOptions { PaidOutOfBand = true });
        }
        catch (Exception e)
        {
            await _btGateway.Transaction.RefundAsync(transactionResult.Target.Id);
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

    private async Task<bool> AttemptToPayInvoiceWithStripeAsync(Invoice invoice)
    {
        try
        {
            var invoiceService = new InvoiceService();
            await invoiceService.PayAsync(invoice.Id);
            return true;
        }
        catch (Exception e)
        {
            _logger.LogWarning(
                e,
                "Exception occurred while trying to pay Stripe invoice with Id: {InvoiceId}",
                invoice.Id);

            throw;
        }
    }

}
