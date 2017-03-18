using Microsoft.AspNetCore.Mvc;

namespace Bit.Billing.Controllers
{
    [Route("stripe")]
    public class StripeController : Controller
    {
        [HttpPost("webhook")]
        public void PostWebhook([FromBody]dynamic body)
        {

        }
    }
}
