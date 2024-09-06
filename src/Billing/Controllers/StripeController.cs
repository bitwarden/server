using Bit.Billing.Models;
using Bit.Billing.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using Event = Stripe.Event;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Bit.Billing.Controllers;

[Route("stripe")]
public class StripeController : Controller
{
    private readonly BillingSettings _billingSettings;
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly ILogger<StripeController> _logger;
    private readonly IStripeEventService _stripeEventService;
    private readonly IStripeEventProcessor _stripeEventProcessor;

    public StripeController(
        IOptions<BillingSettings> billingSettings,
        IWebHostEnvironment hostingEnvironment,
        ILogger<StripeController> logger,
        IStripeEventService stripeEventService,
        IStripeEventProcessor stripeEventProcessor)
    {
        _billingSettings = billingSettings?.Value;
        _hostingEnvironment = hostingEnvironment;
        _logger = logger;
        _stripeEventService = stripeEventService;
        _stripeEventProcessor = stripeEventProcessor;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> PostWebhook([FromQuery] string key)
    {
        if (!CoreHelpers.FixedTimeEquals(key, _billingSettings.StripeWebhookKey))
        {
            _logger.LogError("Stripe webhook key did not match key saved in configuration");
            return new BadRequestResult();
        }

        var parsedEvent = await TryParseEventFromRequestBodyAsync();
        if (parsedEvent is null)
        {
            return Ok();
        }

        if (StripeConfiguration.ApiVersion != parsedEvent.ApiVersion)
        {
            _logger.LogWarning(
                "Stripe {WebhookType} webhook's API version ({WebhookAPIVersion}) does not match SDK API Version ({SDKAPIVersion}) for event ({EventID})",
                parsedEvent.Type,
                parsedEvent.ApiVersion,
                StripeConfiguration.ApiVersion,
                parsedEvent.Id);

            return new OkResult();
        }

        if (string.IsNullOrWhiteSpace(parsedEvent?.Id))
        {
            _logger.LogWarning("No event id.");
            return new BadRequestResult();
        }

        if (_hostingEnvironment.IsProduction() && !parsedEvent.Livemode)
        {
            _logger.LogWarning("Getting test events in production.");
            return new BadRequestResult();
        }

        // If the customer and server cloud regions don't match, early return 200 to avoid unnecessary errors
        if (!await _stripeEventService.ValidateCloudRegion(parsedEvent))
        {
            _logger.LogWarning("Cloud region validation failed for event ({EventID})", parsedEvent.Id);
            return new OkResult();
        }

        await _stripeEventProcessor.ProcessEventAsync(parsedEvent);
        return Ok();
    }

    /// <summary>
    /// Selects the appropriate Stripe webhook secret based on the API version specified in the webhook body.
    /// </summary>
    /// <param name="webhookBody">The body of the webhook request received from Stripe.</param>
    /// <returns>
    /// The Stripe webhook secret corresponding to the API version found in the webhook body.
    /// Returns null if the API version is unrecognized.
    /// </returns>
    private string PickStripeWebhookSecret(string webhookBody)
    {
        var deliveryContainer = JsonSerializer.Deserialize<StripeWebhookDeliveryContainer>(webhookBody);

        _logger.LogInformation("Picking webhook secret for Stripe event ({EventID}) | API Version: {ApiVersion} | Causing Request ID: {RequestID}",
            deliveryContainer.Id,
            deliveryContainer.ApiVersion,
            deliveryContainer.Request.Id);

        return deliveryContainer.ApiVersion switch
        {
            "2024-06-20" => HandleVersionFound("2024-06-20", _billingSettings.StripeWebhookSecret20240620),
            "2023-10-16" => HandleVersionFound("2023-10-16", _billingSettings.StripeWebhookSecret20231016),
            "2022-08-01" => HandleVersionFound("2022-08-01", _billingSettings.StripeWebhookSecret),
            _ => HandleVersionNotFound(deliveryContainer.ApiVersion)
        };

        string HandleVersionFound(string version, string secret)
        {
            if (!string.IsNullOrEmpty(secret))
            {
                var truncatedSecret = secret[..12];

                _logger.LogInformation("Picked webhook secret ({TruncatedSecret}...) for API version {ApiVersion}", truncatedSecret, version);

                return secret;
            }

            _logger.LogError("Stripe webhook contained API version {APIVersion}, but no webhook secret is configured for that version", version);

            return null;
        }

        string HandleVersionNotFound(string version)
        {
            _logger.LogWarning(
                "Stripe webhook contained an unrecognized 'api_version': {ApiVersion}",
                version);

            return null;
        }
    }

    /// <summary>
    /// Attempts to pick the Stripe webhook secret from the JSON payload.
    /// </summary>
    /// <returns>Returns the event if the event was parsed, otherwise, null</returns>
    private async Task<Event> TryParseEventFromRequestBodyAsync()
    {
        using var sr = new StreamReader(HttpContext.Request.Body);

        var json = await sr.ReadToEndAsync();
        var webhookSecret = PickStripeWebhookSecret(json);

        if (string.IsNullOrEmpty(webhookSecret))
        {
            _logger.LogWarning("Failed to pick webhook secret based on event's API version");
            return null;
        }

        var parsedEvent = EventUtility.ConstructEvent(
            json,
            Request.Headers["Stripe-Signature"],
            webhookSecret,
            throwOnApiVersionMismatch: false);

        if (parsedEvent is not null)
        {
            return parsedEvent;
        }

        _logger.LogError("Stripe-Signature request header doesn't match configured Stripe webhook secret");
        return null;
    }
}
