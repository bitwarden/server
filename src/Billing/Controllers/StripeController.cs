using System.Data.SqlClient;
using Bit.Billing.Constants;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using TaxRate = Bit.Core.Entities.TaxRate;

namespace Bit.Billing.Controllers;

[Route("stripe")]
public class StripeController : Controller
{
    private const decimal PremiumPlanAppleIapPrice = 14.99M;
    private const string PremiumPlanId = "premium-annually";
    private const string PremiumPlanIdAppStore = "premium-annually-app";

    private readonly BillingSettings _billingSettings;
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly IOrganizationService _organizationService;
    private readonly IValidateSponsorshipCommand _validateSponsorshipCommand;
    private readonly IOrganizationSponsorshipRenewCommand _organizationSponsorshipRenewCommand;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUserService _userService;
    private readonly IAppleIapService _appleIapService;
    private readonly IMailService _mailService;
    private readonly ILogger<StripeController> _logger;
    private readonly Braintree.BraintreeGateway _btGateway;
    private readonly IReferenceEventService _referenceEventService;
    private readonly ITaxRateRepository _taxRateRepository;
    private readonly IUserRepository _userRepository;

    public StripeController(
        GlobalSettings globalSettings,
        IOptions<BillingSettings> billingSettings,
        IWebHostEnvironment hostingEnvironment,
        IOrganizationService organizationService,
        IValidateSponsorshipCommand validateSponsorshipCommand,
        IOrganizationSponsorshipRenewCommand organizationSponsorshipRenewCommand,
        IOrganizationRepository organizationRepository,
        ITransactionRepository transactionRepository,
        IUserService userService,
        IAppleIapService appleIapService,
        IMailService mailService,
        IReferenceEventService referenceEventService,
        ILogger<StripeController> logger,
        ITaxRateRepository taxRateRepository,
        IUserRepository userRepository)
    {
        _billingSettings = billingSettings?.Value;
        _hostingEnvironment = hostingEnvironment;
        _organizationService = organizationService;
        _validateSponsorshipCommand = validateSponsorshipCommand;
        _organizationSponsorshipRenewCommand = organizationSponsorshipRenewCommand;
        _organizationRepository = organizationRepository;
        _transactionRepository = transactionRepository;
        _userService = userService;
        _appleIapService = appleIapService;
        _mailService = mailService;
        _referenceEventService = referenceEventService;
        _taxRateRepository = taxRateRepository;
        _userRepository = userRepository;
        _logger = logger;
        _btGateway = new Braintree.BraintreeGateway
        {
            Environment = globalSettings.Braintree.Production ?
                Braintree.Environment.PRODUCTION : Braintree.Environment.SANDBOX,
            MerchantId = globalSettings.Braintree.MerchantId,
            PublicKey = globalSettings.Braintree.PublicKey,
            PrivateKey = globalSettings.Braintree.PrivateKey
        };
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> PostWebhook([FromQuery] string key)
    {
        if (!CoreHelpers.FixedTimeEquals(key, _billingSettings.StripeWebhookKey))
        {
            return new BadRequestResult();
        }

        Stripe.Event parsedEvent;
        using (var sr = new StreamReader(HttpContext.Request.Body))
        {
            var json = await sr.ReadToEndAsync();
            parsedEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"],
                _billingSettings.StripeWebhookSecret,
                throwOnApiVersionMismatch: _billingSettings.StripeEventParseThrowMismatch);
        }

        if (string.IsNullOrWhiteSpace(parsedEvent?.Id))
        {
            _logger.LogWarning("No event id.");
            return new BadRequestResult();
        }

        if (_hostingEnvironment.IsProduction() && !parsedEvent.Livemode)
        {
            _logger.LogWarning("Getting test events in production.");
            return new BadRequestResult();
        }

        var subDeleted = parsedEvent.Type.Equals(HandledStripeWebhook.SubscriptionDeleted);
        var subUpdated = parsedEvent.Type.Equals(HandledStripeWebhook.SubscriptionUpdated);

        if (subDeleted || subUpdated)
        {
            var subscription = await GetSubscriptionAsync(parsedEvent, true);
            var ids = GetIdsFromMetaData(subscription.Metadata);

            var subCanceled = subDeleted && subscription.Status == "canceled";
            var subUnpaid = subUpdated && subscription.Status == "unpaid";
            var subIncompleteExpired = subUpdated && subscription.Status == "incomplete_expired";

            if (subCanceled || subUnpaid || subIncompleteExpired)
            {
                // org
                if (ids.Item1.HasValue)
                {
                    await _organizationService.DisableAsync(ids.Item1.Value, subscription.CurrentPeriodEnd);
                }
                // user
                else if (ids.Item2.HasValue)
                {
                    await _userService.DisablePremiumAsync(ids.Item2.Value, subscription.CurrentPeriodEnd);
                }
            }

            if (subUpdated)
            {
                // org
                if (ids.Item1.HasValue)
                {
                    await _organizationService.UpdateExpirationDateAsync(ids.Item1.Value,
                        subscription.CurrentPeriodEnd);
                    if (IsSponsoredSubscription(subscription))
                    {
                        await _organizationSponsorshipRenewCommand.UpdateExpirationDateAsync(ids.Item1.Value, subscription.CurrentPeriodEnd);
                    }
                }
                // user
                else if (ids.Item2.HasValue)
                {
                    await _userService.UpdatePremiumExpirationAsync(ids.Item2.Value,
                        subscription.CurrentPeriodEnd);
                }
            }
        }
        else if (parsedEvent.Type.Equals(HandledStripeWebhook.UpcomingInvoice))
        {
            var invoice = await GetInvoiceAsync(parsedEvent);
            var subscriptionService = new SubscriptionService();
            var subscription = await subscriptionService.GetAsync(invoice.SubscriptionId);
            if (subscription == null)
            {
                throw new Exception("Invoice subscription is null. " + invoice.Id);
            }

            subscription = await VerifyCorrectTaxRateForCharge(invoice, subscription);

            string email = null;
            var ids = GetIdsFromMetaData(subscription.Metadata);
            // org
            if (ids.Item1.HasValue)
            {
                // sponsored org
                if (IsSponsoredSubscription(subscription))
                {
                    await _validateSponsorshipCommand.ValidateSponsorshipAsync(ids.Item1.Value);
                }

                var org = await _organizationRepository.GetByIdAsync(ids.Item1.Value);
                if (org != null && OrgPlanForInvoiceNotifications(org))
                {
                    email = org.BillingEmail;
                }
            }
            // user
            else if (ids.Item2.HasValue)
            {
                var user = await _userService.GetUserByIdAsync(ids.Item2.Value);
                if (user.Premium)
                {
                    email = user.Email;
                }
            }

            if (!string.IsNullOrWhiteSpace(email) && invoice.NextPaymentAttempt.HasValue)
            {
                var items = invoice.Lines.Select(i => i.Description).ToList();
                await _mailService.SendInvoiceUpcomingAsync(email, invoice.AmountDue / 100M,
                    invoice.NextPaymentAttempt.Value, items, true);
            }
        }
        else if (parsedEvent.Type.Equals(HandledStripeWebhook.ChargeSucceeded))
        {
            var charge = await GetChargeAsync(parsedEvent);
            var chargeTransaction = await _transactionRepository.GetByGatewayIdAsync(
                GatewayType.Stripe, charge.Id);
            if (chargeTransaction != null)
            {
                _logger.LogWarning("Charge success already processed. " + charge.Id);
                return new OkResult();
            }

            Tuple<Guid?, Guid?> ids = null;
            Subscription subscription = null;
            var subscriptionService = new SubscriptionService();

            if (charge.InvoiceId != null)
            {
                var invoiceService = new InvoiceService();
                var invoice = await invoiceService.GetAsync(charge.InvoiceId);
                if (invoice?.SubscriptionId != null)
                {
                    subscription = await subscriptionService.GetAsync(invoice.SubscriptionId);
                    ids = GetIdsFromMetaData(subscription?.Metadata);
                }
            }

            if (subscription == null || ids == null || (ids.Item1.HasValue && ids.Item2.HasValue))
            {
                var subscriptions = await subscriptionService.ListAsync(new SubscriptionListOptions
                {
                    Customer = charge.CustomerId
                });
                foreach (var sub in subscriptions)
                {
                    if (sub.Status != "canceled" && sub.Status != "incomplete_expired")
                    {
                        ids = GetIdsFromMetaData(sub.Metadata);
                        if (ids.Item1.HasValue || ids.Item2.HasValue)
                        {
                            subscription = sub;
                            break;
                        }
                    }
                }
            }

            if (!ids.Item1.HasValue && !ids.Item2.HasValue)
            {
                _logger.LogWarning("Charge success has no subscriber ids. " + charge.Id);
                return new BadRequestResult();
            }

            var tx = new Transaction
            {
                Amount = charge.Amount / 100M,
                CreationDate = charge.Created,
                OrganizationId = ids.Item1,
                UserId = ids.Item2,
                Type = TransactionType.Charge,
                Gateway = GatewayType.Stripe,
                GatewayId = charge.Id
            };

            if (charge.Source != null && charge.Source is Card card)
            {
                tx.PaymentMethodType = PaymentMethodType.Card;
                tx.Details = $"{card.Brand}, *{card.Last4}";
            }
            else if (charge.Source != null && charge.Source is BankAccount bankAccount)
            {
                tx.PaymentMethodType = PaymentMethodType.BankAccount;
                tx.Details = $"{bankAccount.BankName}, *{bankAccount.Last4}";
            }
            else if (charge.Source != null && charge.Source is Source source)
            {
                if (source.Card != null)
                {
                    tx.PaymentMethodType = PaymentMethodType.Card;
                    tx.Details = $"{source.Card.Brand}, *{source.Card.Last4}";
                }
                else if (source.AchDebit != null)
                {
                    tx.PaymentMethodType = PaymentMethodType.BankAccount;
                    tx.Details = $"{source.AchDebit.BankName}, *{source.AchDebit.Last4}";
                }
                else if (source.AchCreditTransfer != null)
                {
                    tx.PaymentMethodType = PaymentMethodType.BankAccount;
                    tx.Details = $"ACH => {source.AchCreditTransfer.BankName}, " +
                        $"{source.AchCreditTransfer.AccountNumber}";
                }
            }
            else if (charge.PaymentMethodDetails != null)
            {
                if (charge.PaymentMethodDetails.Card != null)
                {
                    tx.PaymentMethodType = PaymentMethodType.Card;
                    tx.Details = $"{charge.PaymentMethodDetails.Card.Brand?.ToUpperInvariant()}, " +
                        $"*{charge.PaymentMethodDetails.Card.Last4}";
                }
                else if (charge.PaymentMethodDetails.AchDebit != null)
                {
                    tx.PaymentMethodType = PaymentMethodType.BankAccount;
                    tx.Details = $"{charge.PaymentMethodDetails.AchDebit.BankName}, " +
                        $"*{charge.PaymentMethodDetails.AchDebit.Last4}";
                }
                else if (charge.PaymentMethodDetails.AchCreditTransfer != null)
                {
                    tx.PaymentMethodType = PaymentMethodType.BankAccount;
                    tx.Details = $"ACH => {charge.PaymentMethodDetails.AchCreditTransfer.BankName}, " +
                        $"{charge.PaymentMethodDetails.AchCreditTransfer.AccountNumber}";
                }
            }

            if (!tx.PaymentMethodType.HasValue)
            {
                _logger.LogWarning("Charge success from unsupported source/method. " + charge.Id);
                return new OkResult();
            }

            try
            {
                await _transactionRepository.CreateAsync(tx);
            }
            // Catch foreign key violations because user/org could have been deleted.
            catch (SqlException e) when (e.Number == 547) { }
        }
        else if (parsedEvent.Type.Equals(HandledStripeWebhook.ChargeRefunded))
        {
            var charge = await GetChargeAsync(parsedEvent);
            var chargeTransaction = await _transactionRepository.GetByGatewayIdAsync(
                GatewayType.Stripe, charge.Id);
            if (chargeTransaction == null)
            {
                throw new Exception("Cannot find refunded charge. " + charge.Id);
            }

            var amountRefunded = charge.AmountRefunded / 100M;

            if (!chargeTransaction.Refunded.GetValueOrDefault() &&
                chargeTransaction.RefundedAmount.GetValueOrDefault() < amountRefunded)
            {
                chargeTransaction.RefundedAmount = amountRefunded;
                if (charge.Refunded)
                {
                    chargeTransaction.Refunded = true;
                }
                await _transactionRepository.ReplaceAsync(chargeTransaction);

                foreach (var refund in charge.Refunds)
                {
                    var refundTransaction = await _transactionRepository.GetByGatewayIdAsync(
                        GatewayType.Stripe, refund.Id);
                    if (refundTransaction != null)
                    {
                        continue;
                    }

                    await _transactionRepository.CreateAsync(new Transaction
                    {
                        Amount = refund.Amount / 100M,
                        CreationDate = refund.Created,
                        OrganizationId = chargeTransaction.OrganizationId,
                        UserId = chargeTransaction.UserId,
                        Type = TransactionType.Refund,
                        Gateway = GatewayType.Stripe,
                        GatewayId = refund.Id,
                        PaymentMethodType = chargeTransaction.PaymentMethodType,
                        Details = chargeTransaction.Details
                    });
                }
            }
            else
            {
                _logger.LogWarning("Charge refund amount doesn't seem correct. " + charge.Id);
            }
        }
        else if (parsedEvent.Type.Equals(HandledStripeWebhook.PaymentSucceeded))
        {
            var invoice = await GetInvoiceAsync(parsedEvent, true);
            if (invoice.Paid && invoice.BillingReason == "subscription_create")
            {
                var subscriptionService = new SubscriptionService();
                var subscription = await subscriptionService.GetAsync(invoice.SubscriptionId);
                if (subscription?.Status == "active")
                {
                    if (DateTime.UtcNow - invoice.Created < TimeSpan.FromMinutes(1))
                    {
                        await Task.Delay(5000);
                    }

                    var ids = GetIdsFromMetaData(subscription.Metadata);
                    // org
                    if (ids.Item1.HasValue)
                    {
                        if (subscription.Items.Any(i => StaticStore.Plans.Any(p => p.StripePlanId == i.Plan.Id)))
                        {
                            await _organizationService.EnableAsync(ids.Item1.Value, subscription.CurrentPeriodEnd);

                            var organization = await _organizationRepository.GetByIdAsync(ids.Item1.Value);
                            await _referenceEventService.RaiseEventAsync(
                                new ReferenceEvent(ReferenceEventType.Rebilled, organization)
                                {
                                    PlanName = organization?.Plan,
                                    PlanType = organization?.PlanType,
                                    Seats = organization?.Seats,
                                    Storage = organization?.MaxStorageGb,
                                });
                        }
                    }
                    // user
                    else if (ids.Item2.HasValue)
                    {
                        if (subscription.Items.Any(i => i.Plan.Id == PremiumPlanId))
                        {
                            await _userService.EnablePremiumAsync(ids.Item2.Value, subscription.CurrentPeriodEnd);

                            var user = await _userRepository.GetByIdAsync(ids.Item2.Value);
                            await _referenceEventService.RaiseEventAsync(
                                new ReferenceEvent(ReferenceEventType.Rebilled, user)
                                {
                                    PlanName = PremiumPlanId,
                                    Storage = user?.MaxStorageGb,
                                });
                        }
                    }
                }
            }
        }
        else if (parsedEvent.Type.Equals(HandledStripeWebhook.PaymentFailed))
        {
            await HandlePaymentFailed(await GetInvoiceAsync(parsedEvent, true));
        }
        else if (parsedEvent.Type.Equals(HandledStripeWebhook.InvoiceCreated))
        {
            var invoice = await GetInvoiceAsync(parsedEvent, true);
            if (!invoice.Paid && UnpaidAutoChargeInvoiceForSubscriptionCycle(invoice))
            {
                await AttemptToPayInvoiceAsync(invoice);
            }
        }
        else
        {
            _logger.LogWarning("Unsupported event received. " + parsedEvent.Type);
        }

        return new OkResult();
    }

    private Tuple<Guid?, Guid?> GetIdsFromMetaData(IDictionary<string, string> metaData)
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

    private bool OrgPlanForInvoiceNotifications(Organization org)
    {
        switch (org.PlanType)
        {
            case PlanType.FamiliesAnnually:
            case PlanType.TeamsAnnually:
            case PlanType.EnterpriseAnnually:
                return true;
            default:
                return false;
        }
    }

    private async Task<bool> AttemptToPayInvoiceAsync(Invoice invoice)
    {
        var customerService = new CustomerService();
        var customer = await customerService.GetAsync(invoice.CustomerId);
        if (customer?.Metadata?.ContainsKey("appleReceipt") ?? false)
        {
            return await AttemptToPayInvoiceWithAppleReceiptAsync(invoice, customer);
        }
        else if (customer?.Metadata?.ContainsKey("btCustomerId") ?? false)
        {
            return await AttemptToPayInvoiceWithBraintreeAsync(invoice, customer);
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

    private async Task<bool> AttemptToPayInvoiceWithBraintreeAsync(Invoice invoice, Customer customer)
    {
        if (!customer?.Metadata?.ContainsKey("btCustomerId") ?? true)
        {
            return false;
        }

        var subscriptionService = new SubscriptionService();
        var subscription = await subscriptionService.GetAsync(invoice.SubscriptionId);
        var ids = GetIdsFromMetaData(subscription?.Metadata);
        if (!ids.Item1.HasValue && !ids.Item2.HasValue)
        {
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

        var transactionResult = await _btGateway.Transaction.SaleAsync(
            new Braintree.TransactionRequest
            {
                Amount = btInvoiceAmount,
                CustomerId = customer.Metadata["btCustomerId"],
                Options = new Braintree.TransactionOptionsRequest
                {
                    SubmitForSettlement = true,
                    PayPal = new Braintree.TransactionOptionsPayPalRequest
                    {
                        CustomField = $"{btObjIdField}:{btObjId}"
                    }
                },
                CustomFields = new Dictionary<string, string>
                {
                    [btObjIdField] = btObjId.ToString()
                }
            });

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

    private bool UnpaidAutoChargeInvoiceForSubscriptionCycle(Invoice invoice)
    {
        return invoice.AmountDue > 0 && !invoice.Paid && invoice.CollectionMethod == "charge_automatically" &&
            invoice.BillingReason == "subscription_cycle" && invoice.SubscriptionId != null;
    }

    private async Task<Charge> GetChargeAsync(Stripe.Event parsedEvent, bool fresh = false)
    {
        if (!(parsedEvent.Data.Object is Charge eventCharge))
        {
            throw new Exception("Charge is null (from parsed event). " + parsedEvent.Id);
        }
        if (!fresh)
        {
            return eventCharge;
        }
        var chargeService = new ChargeService();
        var charge = await chargeService.GetAsync(eventCharge.Id);
        if (charge == null)
        {
            throw new Exception("Charge is null. " + eventCharge.Id);
        }
        return charge;
    }

    private async Task<Invoice> GetInvoiceAsync(Stripe.Event parsedEvent, bool fresh = false)
    {
        if (!(parsedEvent.Data.Object is Invoice eventInvoice))
        {
            throw new Exception("Invoice is null (from parsed event). " + parsedEvent.Id);
        }
        if (!fresh)
        {
            return eventInvoice;
        }
        var invoiceService = new InvoiceService();
        var invoice = await invoiceService.GetAsync(eventInvoice.Id);
        if (invoice == null)
        {
            throw new Exception("Invoice is null. " + eventInvoice.Id);
        }
        return invoice;
    }

    private async Task<Subscription> GetSubscriptionAsync(Stripe.Event parsedEvent, bool fresh = false)
    {
        if (!(parsedEvent.Data.Object is Subscription eventSubscription))
        {
            throw new Exception("Subscription is null (from parsed event). " + parsedEvent.Id);
        }
        if (!fresh)
        {
            return eventSubscription;
        }
        var subscriptionService = new SubscriptionService();
        var subscription = await subscriptionService.GetAsync(eventSubscription.Id);
        if (subscription == null)
        {
            throw new Exception("Subscription is null. " + eventSubscription.Id);
        }
        return subscription;
    }

    private async Task<Subscription> VerifyCorrectTaxRateForCharge(Invoice invoice, Subscription subscription)
    {
        if (!string.IsNullOrWhiteSpace(invoice?.CustomerAddress?.Country) && !string.IsNullOrWhiteSpace(invoice?.CustomerAddress?.PostalCode))
        {
            var localBitwardenTaxRates = await _taxRateRepository.GetByLocationAsync(
                new TaxRate()
                {
                    Country = invoice.CustomerAddress.Country,
                    PostalCode = invoice.CustomerAddress.PostalCode
                }
            );

            if (localBitwardenTaxRates.Any())
            {
                var stripeTaxRate = await new TaxRateService().GetAsync(localBitwardenTaxRates.First().Id);
                if (stripeTaxRate != null && !subscription.DefaultTaxRates.Any(x => x == stripeTaxRate))
                {
                    subscription.DefaultTaxRates = new List<Stripe.TaxRate> { stripeTaxRate };
                    var subscriptionOptions = new SubscriptionUpdateOptions() { DefaultTaxRates = new List<string>() { stripeTaxRate.Id } };
                    subscription = await new SubscriptionService().UpdateAsync(subscription.Id, subscriptionOptions);
                }
            }
        }
        return subscription;
    }

    private static bool IsSponsoredSubscription(Subscription subscription) =>
        StaticStore.SponsoredPlans.Any(p => p.StripePlanId == subscription.Id);

    private async Task HandlePaymentFailed(Invoice invoice)
    {
        if (!invoice.Paid && invoice.AttemptCount > 1 && UnpaidAutoChargeInvoiceForSubscriptionCycle(invoice))
        {
            var subscriptionService = new SubscriptionService();
            var subscription = await subscriptionService.GetAsync(invoice.SubscriptionId);
            // attempt count 4 = 11 days after initial failure
            if (invoice.AttemptCount > 3 && subscription.Items.Any(i => i.Price.Id == PremiumPlanId || i.Price.Id == PremiumPlanIdAppStore))
            {
                await CancelSubscription(invoice.SubscriptionId);
                await VoidOpenInvoices(invoice.SubscriptionId);
            }
            else
            {
                await AttemptToPayInvoiceAsync(invoice);
            }
        }
    }

    private async Task CancelSubscription(string subscriptionId)
    {
        await new SubscriptionService().CancelAsync(subscriptionId, new SubscriptionCancelOptions());
    }

    private async Task VoidOpenInvoices(string subscriptionId)
    {
        var invoiceService = new InvoiceService();
        var options = new InvoiceListOptions
        {
            Status = "open",
            Subscription = subscriptionId
        };
        var invoices = invoiceService.List(options);
        foreach (var invoice in invoices)
        {
            await invoiceService.VoidInvoiceAsync(invoice.Id);
        }
    }
}
