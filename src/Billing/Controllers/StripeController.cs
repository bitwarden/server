using Bit.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using System;
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

            var parsedEvent = StripeEventUtility.ParseEventDataItem<StripeEvent>(body);
            if(string.IsNullOrWhiteSpace(parsedEvent?.Id))
            {
                return new BadRequestResult();
            }

            if(_hostingEnvironment.IsProduction() && !parsedEvent.LiveMode)
            {
                return new BadRequestResult();
            }

            if(parsedEvent.Type == "customer.subscription.deleted")
            {
                var subscription = Mapper<StripeSubscription>.MapFromJson(parsedEvent.Data.Object.ToString())
                    as StripeSubscription;
                if(subscription?.Status == "canceled")
                {
                    if(subscription.Metadata?.ContainsKey("organizationId") ?? false)
                    {
                        var orgIdGuid = new Guid(subscription.Metadata["organizationId"]);
                        await _organizationService.DisableAsync(orgIdGuid);
                    }
                    else if(subscription.Metadata?.ContainsKey("userId") ?? false)
                    {
                        var userIdGuid = new Guid(subscription.Metadata["userId"]);
                        await _userService.DisablePremiumAsync(userIdGuid);
                    }
                }
            }
            else
            {
                // Not handling this event type.
            }

            return new OkResult();
        }
    }
}
