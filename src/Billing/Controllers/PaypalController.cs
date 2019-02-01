using Bit.Billing.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Bit.Billing.Controllers
{
    [Route("paypal")]
    public class PaypalController : Controller
    {
        private readonly BillingSettings _billingSettings;
        private readonly PaypalClient _paypalClient;

        public PaypalController(
            IOptions<BillingSettings> billingSettings,
            PaypalClient paypalClient)
        {
            _billingSettings = billingSettings?.Value;
            _paypalClient = paypalClient;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> PostWebhook([FromQuery] string key)
        {
            if(HttpContext?.Request == null)
            {
                return new BadRequestResult();
            }

            string body = null;
            using(var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync();
            }

            if(body == null)
            {
                return new BadRequestResult();
            }

            var verified = await _paypalClient.VerifyWebhookAsync(body, HttpContext.Request.Headers,
                _billingSettings.Paypal.WebhookId);
            if(!verified)
            {
                return new BadRequestResult();
            }

            var webhook = JsonConvert.DeserializeObject(body);
            // TODO: process webhook
            return new OkResult();
        }
    }
}
