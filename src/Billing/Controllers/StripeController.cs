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
            _logger.LogError("Stripe webhook key does not match configured webhook key");
            return new BadRequestResult();
        }

        var parsedEvent = await TryParseEventFromRequestBodyAsync();
        if (parsedEvent is null)
        {
            return Ok(new
            {
                Processed = false,
                Message = "Could not find a configured webhook secret to process this event with"
            });
        }

        if (StripeConfiguration.ApiVersion != parsedEvent.ApiVersion)
        {
            _logger.LogWarning(
                "Stripe {WebhookType} webhook's API version ({WebhookAPIVersion}) does not match SDK API Version ({SDKAPIVersion})",
                parsedEvent.Type,
                parsedEvent.ApiVersion,
                StripeConfiguration.ApiVersion);

            return Ok(new
            {
                Processed = false,
                Message = "SDK API version does not match the event's API version"
            });
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
            return Ok(new
            {
                Processed = false,
                Message = "Event is not for this cloud region"
            });
        }

        await _stripeEventProcessor.ProcessEventAsync(parsedEvent);
        return Ok(new
        {
            Processed = true,
            Message = "Processed"
        });
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

        _logger.LogInformation(
            "Picking secret for Stripe webhook | {EventID}: {EventType} | Version: {APIVersion} | Initiating Request ID: {RequestID}",
            deliveryContainer.Id,
            deliveryContainer.Type,
            deliveryContainer.ApiVersion,
            deliveryContainer.Request?.Id);

        return deliveryContainer.ApiVersion switch
        {
            "2024-06-20" => HandleVersionWith(_billingSettings.StripeWebhookSecret20240620),
            "2023-10-16" => HandleVersionWith(_billingSettings.StripeWebhookSecret20231016),
            "2022-08-01" => HandleVersionWith(_billingSettings.StripeWebhookSecret),
            _ => HandleDefault(deliveryContainer.ApiVersion)
        };

        string HandleVersionWith(string secret)
        {
            if (string.IsNullOrEmpty(secret))
            {
                _logger.LogError("No webhook secret is configured for API version {APIVersion}", deliveryContainer.ApiVersion);
                return null;
            }

            if (!secret.StartsWith("whsec_"))
            {
                _logger.LogError("Webhook secret configured for API version {APIVersion} does not start with whsec_",
                    deliveryContainer.ApiVersion);
                return null;
            }

            var truncatedSecret = secret[..10];

            _logger.LogInformation("Picked webhook secret {TruncatedSecret}... for API version {APIVersion}", truncatedSecret, deliveryContainer.ApiVersion);

            return secret;
        }

        string HandleDefault(string version)
        {
            _logger.LogWarning(
                "Stripe webhook contained an API version ({APIVersion}) we do not process",
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
            return null;
        }

        return EventUtility.ConstructEvent(
            json,
            Request.Headers["Stripe-Signature"],
            webhookSecret,
            throwOnApiVersionMismatch: false);
    }
}
