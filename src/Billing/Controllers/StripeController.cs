using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bit.Billing.Controllers
{
    [Route("stripe")]
    public class StripeController : Controller
    {
        private readonly BillingSettings _billingSettings;

        public StripeController(IOptions<BillingSettings> billingSettings)
        {
            _billingSettings = billingSettings?.Value;
        }

        [HttpPost("webhook")]
        public IActionResult PostWebhook([FromBody]dynamic body, [FromQuery] string key)
        {
            if(key != _billingSettings.StripeWebhookKey)
            {
                return new BadRequestResult();
            }

            return new OkResult();
        }
    }
}
