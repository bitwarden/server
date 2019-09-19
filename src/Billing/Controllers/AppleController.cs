using Bit.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Bit.Billing.Controllers
{
    [Route("apple")]
    public class AppleController : Controller
    {
        private readonly BillingSettings _billingSettings;
        private readonly ILogger<AppleController> _logger;

        public AppleController(
            IOptions<BillingSettings> billingSettings,
            ILogger<AppleController> logger)
        {
            _billingSettings = billingSettings?.Value;
            _logger = logger;
        }

        [HttpPost("iap")]
        public async Task<IActionResult> PostIap()
        {
            if(HttpContext?.Request?.Query == null)
            {
                return new BadRequestResult();
            }

            var key = HttpContext.Request.Query.ContainsKey("key") ?
                HttpContext.Request.Query["key"].ToString() : null;
            if(key != _billingSettings.AppleWebhookKey)
            {
                return new BadRequestResult();
            }

            string body = null;
            using(var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync();
            }

            if(string.IsNullOrWhiteSpace(body))
            {
                return new BadRequestResult();
            }

            try
            {
                var json = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(body), Formatting.Indented);
                _logger.LogInformation("Apple IAP Notification:\n\n" + Constants.BypassFiltersEventId, json);
                return new OkResult();
            }
            catch(Exception e)
            {
                _logger.LogError(e, "Error processing IAP status notification.");
                return new BadRequestResult();
            }
        }
    }
}
