using System.Text;
using System.Text.Json;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bit.Billing.Controllers;

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
        if (HttpContext?.Request?.Query == null)
        {
            return new BadRequestResult();
        }

        var key = HttpContext.Request.Query.ContainsKey("key") ?
            HttpContext.Request.Query["key"].ToString() : null;
        if (!CoreHelpers.FixedTimeEquals(key, _billingSettings.AppleWebhookKey))
        {
            return new BadRequestResult();
        }

        string body = null;
        using (var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync();
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new BadRequestResult();
        }

        try
        {
            var json = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonDocument>(body), JsonHelpers.Indented);
            _logger.LogInformation(Bit.Core.Constants.BypassFiltersEventId, "Apple IAP Notification:\n\n{0}", json);
            return new OkResult();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error processing IAP status notification.");
            return new BadRequestResult();
        }
    }
}
