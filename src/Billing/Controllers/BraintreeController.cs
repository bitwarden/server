using Bit.Core;
using Bit.Core.Services;
using Braintree;
using Braintree.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace Bit.Billing.Controllers
{
    [Route("braintree")]
    public class BraintreeController : Controller
    {
        private static BraintreeGateway _gateway;
        private readonly BillingSettings _billingSettings;
        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly IOrganizationService _organizationService;
        private readonly IUserService _userService;

        public BraintreeController(
            IOptions<BillingSettings> billingSettings,
            GlobalSettings globalSettings,
            IHostingEnvironment hostingEnvironment,
            IOrganizationService organizationService,
            IUserService userService)
        {
            if(_gateway == null)
            {
                _gateway = new BraintreeGateway
                {
                    Environment = globalSettings.Braintree.Production ?
                        Braintree.Environment.PRODUCTION : Braintree.Environment.SANDBOX,
                    MerchantId = globalSettings.Braintree.MerchantId,
                    PublicKey = globalSettings.Braintree.PublicKey,
                    PrivateKey = globalSettings.Braintree.PrivateKey
                };
            }

            _billingSettings = billingSettings?.Value;
            _hostingEnvironment = hostingEnvironment;
            _organizationService = organizationService;
            _userService = userService;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> PostWebhook([FromQuery] string key)
        {
            if(key != _billingSettings.BraintreeWebhookKey)
            {
                return new BadRequestResult();
            }

            var payload = Request.Form["bt_payload"];
            var signature = Request.Form["bt_signature"];

            WebhookNotification notification;
            try
            {
                notification = _gateway.WebhookNotification.Parse(signature, payload);
            }
            catch(InvalidSignatureException)
            {
                return new BadRequestResult();
            }

            if(notification.Kind == WebhookKind.SUBSCRIPTION_CANCELED ||
                notification.Kind == WebhookKind.SUBSCRIPTION_WENT_ACTIVE ||
                notification.Kind == WebhookKind.SUBSCRIPTION_WENT_PAST_DUE ||
                notification.Kind == WebhookKind.SUBSCRIPTION_CHARGED_SUCCESSFULLY ||
                notification.Kind == WebhookKind.SUBSCRIPTION_CHARGED_UNSUCCESSFULLY ||
                notification.Kind == WebhookKind.SUBSCRIPTION_EXPIRED ||
                notification.Kind == WebhookKind.SUBSCRIPTION_TRIAL_ENDED)
            {
                var ids = GetIdsFromId(notification.Subscription.Id);

                if((notification.Kind == WebhookKind.SUBSCRIPTION_CANCELED ||
                    notification.Kind == WebhookKind.SUBSCRIPTION_EXPIRED) &&
                    (notification.Subscription.Status == SubscriptionStatus.CANCELED ||
                    notification.Subscription.Status == SubscriptionStatus.EXPIRED))
                {
                    // org
                    if(ids.Item1.HasValue)
                    {
                        await _organizationService.DisableAsync(ids.Item1.Value,
                            notification.Subscription.BillingPeriodEndDate);
                    }
                    // user
                    else if(ids.Item2.HasValue)
                    {
                        await _userService.DisablePremiumAsync(ids.Item2.Value,
                            notification.Subscription.BillingPeriodEndDate);
                    }
                }
                else
                {
                    // A race condition is happening between the time of purchase and receiving this webhook. Add some
                    // artificial delay here to help combat that.
                    await Task.Delay(2000);

                    // org
                    if(ids.Item1.HasValue)
                    {
                        await _organizationService.UpdateExpirationDateAsync(ids.Item1.Value,
                            notification.Subscription.BillingPeriodEndDate);
                    }
                    // user
                    else if(ids.Item2.HasValue)
                    {
                        await _userService.UpdatePremiumExpirationAsync(ids.Item2.Value,
                            notification.Subscription.BillingPeriodEndDate);
                    }
                }
            }

            return new OkResult();
        }

        private Tuple<Guid?, Guid?> GetIdsFromId(string id)
        {
            Guid? orgId = null;
            Guid? userId = null;

            if(id.Length >= 33)
            {
                var type = id[0];
                var entityIdString = id.Substring(1, 32);
                Guid entityId;
                if(Guid.TryParse(entityIdString, out entityId))
                {
                    if(type == 'o')
                    {
                        orgId = entityId;
                    }
                    else if(type == 'u')
                    {
                        userId = entityId;
                    }
                }
            }

            return new Tuple<Guid?, Guid?>(orgId, userId);
        }
    }
}
