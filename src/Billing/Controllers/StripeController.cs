using Bit.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using System;
using System.Collections.Generic;
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
        public async Task<IActionResult> PostWebhook([FromBody]dynamic body, [FromQuery] string key)
        {
            if(body == null || key != _billingSettings.StripeWebhookKey)
            {
                return new BadRequestResult();
            }

            StripeEvent parsedEvent = StripeEventUtility.ParseEventDataItem<StripeEvent>(body);
            if(string.IsNullOrWhiteSpace(parsedEvent?.Id))
            {
                return new BadRequestResult();
            }

            if(_hostingEnvironment.IsProduction() && !parsedEvent.LiveMode)
            {
                return new BadRequestResult();
            }

            if(parsedEvent.Type.Equals("customer.subscription.deleted") ||
                parsedEvent.Type.Equals("customer.subscription.updated"))
            {
                StripeSubscription subscription = Mapper<StripeSubscription>.MapFromJson(parsedEvent.Data.Object.ToString());
                var ids = GetIdsFromMetaData(subscription.Metadata);

                if(parsedEvent.Type.Equals("customer.subscription.deleted") && subscription?.Status == "canceled")
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
                else if(parsedEvent.Type.Equals("customer.subscription.updated"))
                {
                    // org
                    if(ids.Item1.HasValue)
                    {
                        await _organizationService.UpdateExpirationDateAsync(ids.Item1.Value, subscription.CurrentPeriodEnd);
                    }
                    // user
                    else if(ids.Item2.HasValue)
                    {
                        await _userService.UpdatePremiumExpirationAsync(ids.Item2.Value, subscription.CurrentPeriodEnd);
                    }
                }
            }

            return new OkResult();
        }

        private Tuple<Guid?, Guid?> GetIdsFromMetaData(IDictionary<string, string> metaData)
        {
            Guid? orgId = null;
            Guid? userId = null;
            if(metaData?.ContainsKey("organizationId") ?? false)
            {
                orgId = new Guid(metaData["organizationId"]);
            }
            else if(metaData?.ContainsKey("userId") ?? false)
            {
                userId = new Guid(metaData["userId"]);
            }

            return new Tuple<Guid?, Guid?>(orgId, userId);
        }
    }
}
