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
        private readonly IUserService _userService;

        public StripeController(
            IOptions<BillingSettings> billingSettings,
            IHostingEnvironment hostingEnvironment,
            IOrganizationService organizationService,
            IUserService userService)
        {
            _billingSettings = billingSettings?.Value;
            _hostingEnvironment = hostingEnvironment;
            _organizationService = organizationService;
            _userService = userService;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> PostWebhook([FromQuery] string key)
        {
            if(key != _billingSettings.StripeWebhookKey)
            {
                return new BadRequestResult();
            }

            StripeEvent parsedEvent;
            using(var sr = new StreamReader(HttpContext.Request.Body))
            {
                var json = await sr.ReadToEndAsync();
                parsedEvent = StripeEventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"],
                    _billingSettings.StripeWebhookSecret);
            }

            if(string.IsNullOrWhiteSpace(parsedEvent?.Id))
            {
                return new BadRequestResult();
            }

            if(_hostingEnvironment.IsProduction() && !parsedEvent.LiveMode)
            {
                return new BadRequestResult();
            }

            var invUpcoming = parsedEvent.Type.Equals("invoice.upcoming");
            var subDeleted = parsedEvent.Type.Equals("customer.subscription.deleted");
            var subUpdated = parsedEvent.Type.Equals("customer.subscription.updated");

            if(subDeleted || subUpdated)
            {
                StripeSubscription subscription = Mapper<StripeSubscription>.MapFromJson(
                    parsedEvent.Data.Object.ToString());
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
            else if(false /* Disabled for now */ && invUpcoming)
            {
                StripeInvoice invoice = Mapper<StripeInvoice>.MapFromJson(
                    parsedEvent.Data.Object.ToString());
                if(invoice == null)
                {
                    throw new Exception("Invoice is null.");
                }

                // TODO: maybe invoice subscription expandable is already here any we don't need to call API?
                var subscriptionService = new StripeSubscriptionService();
                var subscription = await subscriptionService.GetAsync(invoice.SubscriptionId);
                if(subscription == null)
                {
                    throw new Exception("Invoice subscription is null.");
                }

                var ids = GetIdsFromMetaData(subscription.Metadata);

                // To include in email:
                // invoice.AmountDue;
                // invoice.DueDate;

                // org
                if(ids.Item1.HasValue)
                {
                    // TODO: email billing contact
                }
                // user
                else if(ids.Item2.HasValue)
                {
                    // TODO: email user
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
    }
}
