using Bit.Billing.Models.Events;
using Bit.Billing.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Bit.Billing.Controllers;

[Route("stripe/events")]
[SelfHosted(NotSelfHostedOnly = true)]
public class StripeEventsController(
    IStripeEventProcessor stripeEventProcessor,
    IStripeFacade stripeFacade,
    IWebHostEnvironment webHostEnvironment) : Controller
{
    private readonly string _stripeURL = webHostEnvironment.IsDevelopment() || webHostEnvironment.IsEnvironment("QA")
        ? "https://dashboard.stripe.com/test"
        : "https://dashboard.stripe.com";

    [HttpPost("inspect")]
    public async Task<Ok<EventsResponseBody>> InspectEventsAsync([FromBody] EventIDsRequestBody requestBody)
    {
        var inspected = await Task.WhenAll(requestBody.EventIDs.Select(async eventId =>
        {
            var @event = await stripeFacade.GetEvent(eventId);

            return Map(@event);
        }));

        var response = new EventsResponseBody { Events = inspected };

        return TypedResults.Ok(response);
    }

    [HttpPost("process")]
    public async Task<Ok<EventsResponseBody>> ProcessEventsAsync([FromBody] EventIDsRequestBody requestBody)
    {
        var processed = await Task.WhenAll(requestBody.EventIDs.Select(async eventId =>
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

        var response = new EventsResponseBody { Events = processed };

        return TypedResults.Ok(response);
    }

    private EventResponseBody Map(Event @event, string processingError = null) => new ()
    {
        Id = @event.Id,
        URL = $"{_stripeURL}/workbench/events/{@event.Id}",
        APIVersion = @event.ApiVersion,
        Type = @event.Type,
        CreatedUTC = @event.Created,
        ProcessingError = processingError
    };
}
