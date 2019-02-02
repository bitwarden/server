using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using System;
using System.Collections.Generic;
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

        public StripeController(
            IOptions<BillingSettings> billingSettings,
            IHostingEnvironment hostingEnvironment,
            IOrganizationService organizationService,
            IOrganizationRepository organizationRepository,
            ITransactionRepository transactionRepository,
            IUserService userService,
            IMailService mailService)
        {
            _billingSettings = billingSettings?.Value;
            _hostingEnvironment = hostingEnvironment;
            _organizationService = organizationService;
            _organizationRepository = organizationRepository;
            _transactionRepository = transactionRepository;
            _userService = userService;
            _mailService = mailService;
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
                return new BadRequestResult();
            }

            if(_hostingEnvironment.IsProduction() && !parsedEvent.Livemode)
            {
                return new BadRequestResult();
            }

            var subDeleted = parsedEvent.Type.Equals("customer.subscription.deleted");
            var subUpdated = parsedEvent.Type.Equals("customer.subscription.updated");

            if(subDeleted || subUpdated)
            {
                var subscription = parsedEvent.Data.Object as Subscription;
                if(subscription == null)
                {
                    throw new Exception("Subscription is null.");
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
                var invoice = parsedEvent.Data.Object as Invoice;
                if(invoice == null)
                {
                    throw new Exception("Invoice is null.");
                }

                var subscriptionService = new SubscriptionService();
                var subscription = await subscriptionService.GetAsync(invoice.SubscriptionId);
                if(subscription == null)
                {
                    throw new Exception("Invoice subscription is null.");
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
                        invoice.NextPaymentAttempt.Value, items, ids.Item1.HasValue);
                }
            }
            else if(parsedEvent.Type.Equals("charge.succeeded"))
            {
                var charge = parsedEvent.Data.Object as Charge;
                if(charge == null)
                {
                    throw new Exception("Charge is null.");
                }

                if(charge.InvoiceId == null)
                {
                    return new OkResult();
                }

                var chargeTransaction = await _transactionRepository.GetByGatewayIdAsync(
                    GatewayType.Stripe, charge.Id);
                if(chargeTransaction == null)
                {
                    var invoiceService = new InvoiceService();
                    var invoice = await invoiceService.GetAsync(charge.InvoiceId);
                    if(invoice == null)
                    {
                        return new OkResult();
                    }

                    var subscriptionService = new SubscriptionService();
                    var subscription = await subscriptionService.GetAsync(invoice.SubscriptionId);
                    if(subscription == null)
                    {
                        return new OkResult();
                    }

                    var ids = GetIdsFromMetaData(subscription.Metadata);
                    if(ids.Item1.HasValue || ids.Item2.HasValue)
                    {
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
                        else
                        {
                            return new OkResult();
                        }

                        await _transactionRepository.CreateAsync(tx);
                    }
                }
            }
            else if(parsedEvent.Type.Equals("charge.refunded"))
            {
                var charge = parsedEvent.Data.Object as Charge;
                if(charge == null)
                {
                    throw new Exception("Charge is null.");
                }

                var chargeTransaction = await _transactionRepository.GetByGatewayIdAsync(
                    GatewayType.Stripe, charge.Id);
                if(chargeTransaction == null)
                {
                    throw new Exception("Cannot find refunded charge.");
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
            }

            return new OkResult();
        }

        private Tuple<Guid?, Guid?> GetIdsFromMetaData(IDictionary<string, string> metaData)
        {
            if(metaData == null)
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
                case Core.Enums.PlanType.FamiliesAnnually:
                case Core.Enums.PlanType.TeamsAnnually:
                case Core.Enums.PlanType.EnterpriseAnnually:
                    return true;
                default:
                    return false;
            }
        }
    }
}
