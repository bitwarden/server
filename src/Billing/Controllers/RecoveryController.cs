using Bit.Billing.Models.Recovery;
using Bit.Billing.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Bit.Billing.Controllers;

[Route("stripe/recovery")]
[SelfHosted(NotSelfHostedOnly = true)]
public class RecoveryController(
    IStripeEventProcessor stripeEventProcessor,
    IStripeFacade stripeFacade,
    IWebHostEnvironment webHostEnvironment) : Controller
{
    private readonly string _stripeURL = webHostEnvironment.IsDevelopment() || webHostEnvironment.IsEnvironment("QA")
        ? "https://dashboard.stripe.com/test"
        : "https://dashboard.stripe.com";

    // ReSharper disable once RouteTemplates.ActionRoutePrefixCanBeExtractedToControllerRoute
    [HttpPost("events/inspect")]
    public async Task<Ok<EventsResponseBody>> InspectEventsAsync([FromBody] EventsRequestBody requestBody)
    {
        var inspected = await Task.WhenAll(requestBody.EventIds.Select(async eventId =>
        {
            var @event = await stripeFacade.GetEvent(eventId);
            return Map(@event);
        }));

        var response = new EventsResponseBody { Events = inspected.ToList() };

        return TypedResults.Ok(response);
    }

    // ReSharper disable once RouteTemplates.ActionRoutePrefixCanBeExtractedToControllerRoute
    [HttpPost("events/process")]
    public async Task<Ok<EventsResponseBody>> ProcessEventsAsync([FromBody] EventsRequestBody requestBody)
    {
        var processed = await Task.WhenAll(requestBody.EventIds.Select(async eventId =>
        {
            var @event = await stripeFacade.GetEvent(eventId);
            try
            {
                await stripeEventProcessor.ProcessEventAsync(@event);
                return Map(@event);
            }
            catch (Exception exception)
            {
                return Map(@event, exception.Message);
            }
        }));

        var response = new EventsResponseBody { Events = processed.ToList() };

        return TypedResults.Ok(response);
    }

    private EventResponseBody Map(Event @event, string processingError = null) => new()
    {
        Id = @event.Id,
        URL = $"{_stripeURL}/workbench/events/{@event.Id}",
        APIVersion = @event.ApiVersion,
        Type = @event.Type,
        CreatedUTC = @event.Created,
        ProcessingError = processingError
    };
}
