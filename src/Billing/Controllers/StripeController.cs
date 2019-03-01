using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Bit.Billing.Controllers
{
    [Route("stripe")]
    public class StripeController : Controller
    {
        private readonly BillingSettings _billingSettings;
        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly IOrganizationService _organizationService;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IUserService _userService;
        private readonly IMailService _mailService;
        private readonly ILogger<StripeController> _logger;
        private readonly Braintree.BraintreeGateway _btGateway;

        public StripeController(
            GlobalSettings globalSettings,
            IOptions<BillingSettings> billingSettings,
            IHostingEnvironment hostingEnvironment,
            IOrganizationService organizationService,
            IOrganizationRepository organizationRepository,
            ITransactionRepository transactionRepository,
            IUserService userService,
            IMailService mailService,
            ILogger<StripeController> logger)
        {
            _billingSettings = billingSettings?.Value;
            _hostingEnvironment = hostingEnvironment;
            _organizationService = organizationService;
            _organizationRepository = organizationRepository;
            _transactionRepository = transactionRepository;
            _userService = userService;
            _mailService = mailService;
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
            if(key != _billingSettings.StripeWebhookKey)
            {
                return new BadRequestResult();
            }

            Stripe.Event parsedEvent;
            using(var sr = new StreamReader(HttpContext.Request.Body))
            {
                var json = await sr.ReadToEndAsync();
                parsedEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"],
                    _billingSettings.StripeWebhookSecret);
            }

            if(string.IsNullOrWhiteSpace(parsedEvent?.Id))
            {
                _logger.LogWarning("No event id.");
                return new BadRequestResult();
            }

            if(_hostingEnvironment.IsProduction() && !parsedEvent.Livemode)
            {
                _logger.LogWarning("Getting test events in production.");
                return new BadRequestResult();
            }

            var subDeleted = parsedEvent.Type.Equals("customer.subscription.deleted");
            var subUpdated = parsedEvent.Type.Equals("customer.subscription.updated");

            if(subDeleted || subUpdated)
            {
                if(!(parsedEvent.Data.Object is Subscription subscription))
                {
                    throw new Exception("Subscription is null. " + parsedEvent.Id);
                }

                var ids = GetIdsFromMetaData(subscription.Metadata);

                var subCanceled = subDeleted && subscription.Status == "canceled";
                var subUnpaid = subUpdated && subscription.Status == "unpaid";

                if(subCanceled || subUnpaid)
                {
                    // org
                    if(ids.Item1.HasValue)
                    {
                        await _organizationService.DisableAsync(ids.Item1.Value, subscription.CurrentPeriodEnd);
                    }
                    // user
                    else if(ids.Item2.HasValue)
                    {
                        await _userService.DisablePremiumAsync(ids.Item2.Value, subscription.CurrentPeriodEnd);
                    }
                }

                if(subUpdated)
                {
                    // org
                    if(ids.Item1.HasValue)
                    {
                        await _organizationService.UpdateExpirationDateAsync(ids.Item1.Value,
                            subscription.CurrentPeriodEnd);
                    }
                    // user
                    else if(ids.Item2.HasValue)
                    {
                        await _userService.UpdatePremiumExpirationAsync(ids.Item2.Value,
                            subscription.CurrentPeriodEnd);
                    }
                }
            }
            else if(parsedEvent.Type.Equals("invoice.upcoming"))
            {
                if(!(parsedEvent.Data.Object is Invoice invoice))
                {
                    throw new Exception("Invoice is null. " + parsedEvent.Id);
                }

                var subscriptionService = new SubscriptionService();
                var subscription = await subscriptionService.GetAsync(invoice.SubscriptionId);
                if(subscription == null)
                {
                    throw new Exception("Invoice subscription is null. " + invoice.Id);
                }

                string email = null;
                var ids = GetIdsFromMetaData(subscription.Metadata);
                // org
                if(ids.Item1.HasValue)
                {
                    var org = await _organizationRepository.GetByIdAsync(ids.Item1.Value);
                    if(org != null && OrgPlanForInvoiceNotifications(org))
                    {
                        email = org.BillingEmail;
                    }
                }
                // user
                else if(ids.Item2.HasValue)
                {
                    var user = await _userService.GetUserByIdAsync(ids.Item2.Value);
                    if(user.Premium)
                    {
                        email = user.Email;
                    }
                }

                if(!string.IsNullOrWhiteSpace(email) && invoice.NextPaymentAttempt.HasValue)
                {
                    var items = invoice.Lines.Select(i => i.Description).ToList();
                    await _mailService.SendInvoiceUpcomingAsync(email, invoice.AmountDue / 100M,
                        invoice.NextPaymentAttempt.Value, items, true);
                }
            }
            else if(parsedEvent.Type.Equals("charge.succeeded"))
            {
                if(!(parsedEvent.Data.Object is Charge charge))
                {
                    throw new Exception("Charge is null. " + parsedEvent.Id);
                }

                var chargeTransaction = await _transactionRepository.GetByGatewayIdAsync(
                    GatewayType.Stripe, charge.Id);
                if(chargeTransaction != null)
                {
                    _logger.LogWarning("Charge success already processed. " + charge.Id);
                    return new OkResult();
                }

                Tuple<Guid?, Guid?> ids = null;
                Subscription subscription = null;
                var subscriptionService = new SubscriptionService();

                if(charge.InvoiceId != null)
                {
                    var invoiceService = new InvoiceService();
                    var invoice = await invoiceService.GetAsync(charge.InvoiceId);
                    if(invoice?.SubscriptionId != null)
                    {
                        subscription = await subscriptionService.GetAsync(invoice.SubscriptionId);
                        ids = GetIdsFromMetaData(subscription?.Metadata);
                    }
                }

                if(subscription == null || ids == null || (ids.Item1.HasValue && ids.Item2.HasValue))
                {
                    var subscriptions = await subscriptionService.ListAsync(new SubscriptionListOptions
                    {
                        CustomerId = charge.CustomerId
                    });
                    foreach(var sub in subscriptions)
                    {
                        if(sub.Status != "canceled")
                        {
                            ids = GetIdsFromMetaData(sub.Metadata);
                            if(ids.Item1.HasValue || ids.Item2.HasValue)
                            {
                                subscription = sub;
                                break;
                            }
                        }
                    }
                }

                if(!ids.Item1.HasValue && !ids.Item2.HasValue)
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

                if(charge.Source is Card card)
                {
                    tx.PaymentMethodType = PaymentMethodType.Card;
                    tx.Details = $"{card.Brand}, *{card.Last4}";
                }
                else if(charge.Source is BankAccount bankAccount)
                {
                    tx.PaymentMethodType = PaymentMethodType.BankAccount;
                    tx.Details = $"{bankAccount.BankName}, *{bankAccount.Last4}";
                }
                else if(charge.Source is Source source)
                {
                    if(source.Card != null)
                    {
                        tx.PaymentMethodType = PaymentMethodType.Card;
                        tx.Details = $"{source.Card.Brand}, *{source.Card.Last4}";
                    }
                    else if(source.AchDebit != null)
                    {
                        tx.PaymentMethodType = PaymentMethodType.BankAccount;
                        tx.Details = $"{source.AchDebit.BankName}, *{source.AchDebit.Last4}";
                    }
                    else if(source.AchCreditTransfer != null)
                    {
                        tx.PaymentMethodType = PaymentMethodType.BankAccount;
                        tx.Details = $"ACH => {source.AchCreditTransfer.BankName}, " +
                            $"{source.AchCreditTransfer.AccountNumber}";
                    }
                }

                if(!tx.PaymentMethodType.HasValue)
                {
                    _logger.LogWarning("Charge success from unsupported source. " + charge.Id);
                    return new OkResult();
                }

                try
                {
                    await _transactionRepository.CreateAsync(tx);
                }
                // Catch foreign key violations because user/org could have been deleted.
                catch(SqlException e) when(e.Number == 547) { }
            }
            else if(parsedEvent.Type.Equals("charge.refunded"))
            {
                if(!(parsedEvent.Data.Object is Charge charge))
                {
                    throw new Exception("Charge is null. " + parsedEvent.Id);
                }

                var chargeTransaction = await _transactionRepository.GetByGatewayIdAsync(
                    GatewayType.Stripe, charge.Id);
                if(chargeTransaction == null)
                {
                    throw new Exception("Cannot find refunded charge. " + charge.Id);
                }

                var amountRefunded = charge.AmountRefunded / 100M;

                if(!chargeTransaction.Refunded.GetValueOrDefault() &&
                    chargeTransaction.RefundedAmount.GetValueOrDefault() < amountRefunded)
                {
                    chargeTransaction.RefundedAmount = amountRefunded;
                    if(charge.Refunded)
                    {
                        chargeTransaction.Refunded = true;
                    }
                    await _transactionRepository.ReplaceAsync(chargeTransaction);

                    foreach(var refund in charge.Refunds)
                    {
                        var refundTransaction = await _transactionRepository.GetByGatewayIdAsync(
                            GatewayType.Stripe, refund.Id);
                        if(refundTransaction != null)
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
            else if(parsedEvent.Type.Equals("invoice.payment_failed"))
            {
                if(!(parsedEvent.Data.Object is Invoice invoice))
                {
                    throw new Exception("Invoice is null. " + parsedEvent.Id);
                }

                if(invoice.AttemptCount > 1 && UnpaidAutoChargeInvoiceForSubscriptionCycle(invoice))
                {
                    await AttemptToPayInvoiceWithBraintreeAsync(invoice);
                }
            }
            else if(parsedEvent.Type.Equals("invoice.created"))
            {
                if(!(parsedEvent.Data.Object is Invoice invoice))
                {
                    throw new Exception("Invoice is null. " + parsedEvent.Id);
                }

                if(UnpaidAutoChargeInvoiceForSubscriptionCycle(invoice))
                {
                    await AttemptToPayInvoiceWithBraintreeAsync(invoice);
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
            if(metaData == null || !metaData.Any())
            {
                return new Tuple<Guid?, Guid?>(null, null);
            }

            Guid? orgId = null;
            Guid? userId = null;

            if(metaData.ContainsKey("organizationId"))
            {
                orgId = new Guid(metaData["organizationId"]);
            }
            else if(metaData.ContainsKey("userId"))
            {
                userId = new Guid(metaData["userId"]);
            }

            if(userId == null && orgId == null)
            {
                var orgIdKey = metaData.Keys.FirstOrDefault(k => k.ToLowerInvariant() == "organizationid");
                if(!string.IsNullOrWhiteSpace(orgIdKey))
                {
                    orgId = new Guid(metaData[orgIdKey]);
                }
                else
                {
                    var userIdKey = metaData.Keys.FirstOrDefault(k => k.ToLowerInvariant() == "userid");
                    if(!string.IsNullOrWhiteSpace(userIdKey))
                    {
                        userId = new Guid(metaData[userIdKey]);
                    }
                }
            }

            return new Tuple<Guid?, Guid?>(orgId, userId);
        }

        private bool OrgPlanForInvoiceNotifications(Organization org)
        {
            switch(org.PlanType)
            {
                case PlanType.FamiliesAnnually:
                case PlanType.TeamsAnnually:
                case PlanType.EnterpriseAnnually:
                    return true;
                default:
                    return false;
            }
        }

        private async Task<bool> AttemptToPayInvoiceWithBraintreeAsync(Invoice invoice)
        {
            var customerService = new CustomerService();
            var customer = await customerService.GetAsync(invoice.CustomerId);
            if(!customer?.Metadata?.ContainsKey("btCustomerId") ?? true)
            {
                return false;
            }

            var subscriptionService = new SubscriptionService();
            var subscription = await subscriptionService.GetAsync(invoice.SubscriptionId);
            var ids = GetIdsFromMetaData(subscription?.Metadata);
            if(!ids.Item1.HasValue && !ids.Item2.HasValue)
            {
                return false;
            }

            var btObjIdField = ids.Item1.HasValue ? "organization_id" : "user_id";
            var btObjId = ids.Item1 ?? ids.Item2.Value;
            var btInvoiceAmount = (invoice.AmountDue / 100M);

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

            if(!transactionResult.IsSuccess())
            {
                if(invoice.AttemptCount < 4)
                {
                    await _mailService.SendPaymentFailedAsync(customer.Email, btInvoiceAmount, true);
                }
                return false;
            }

            try
            {
                var invoiceService = new InvoiceService();
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
            catch(Exception e)
            {
                await _btGateway.Transaction.RefundAsync(transactionResult.Target.Id);
                throw e;
            }

            return true;
        }

        private bool UnpaidAutoChargeInvoiceForSubscriptionCycle(Invoice invoice)
        {
            return invoice.AmountDue > 0 && !invoice.Paid && invoice.Billing == Stripe.Billing.ChargeAutomatically &&
                invoice.BillingReason == "subscription_cycle" && invoice.SubscriptionId != null;
        }
    }
}
