using System.Collections.Concurrent;
using Bit.Billing.Models;
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
    public async Task<Ok<InspectEventsResponseBody>> InspectEventsAsync([FromBody] EventIDsRequestBody requestBody)
    {
        var inspected = new ConcurrentBag<EventResponseBody>();

        await Parallel.ForEachAsync(requestBody.EventIDs, async (eventId, cancellationToken) =>
        {
            var @event = await stripeFacade.GetEvent(eventId, cancellationToken: cancellationToken);

            inspected.Add(Map(@event));
        });

        var response = new InspectEventsResponseBody { Events = inspected.ToList() };

        return TypedResults.Ok(response);
    }

    [HttpPost]
    public async Task<Ok<ProcessEventsResponseBody>> ProcessEventsAsync([FromBody] EventIDsRequestBody requestBody)
    {
        var successful = new ConcurrentBag<EventResponseBody>();

        var failed = new ConcurrentBag<EventResponseBody>();

        await Parallel.ForEachAsync(requestBody.EventIDs, async (eventId, cancellationToken) =>
        {
            var @event = await stripeFacade.GetEvent(eventId, cancellationToken: cancellationToken);

            try
            {
                await stripeEventProcessor.ProcessEventAsync(@event);

                successful.Add(Map(@event));
            }
            catch (Exception exception)
            {
                failed.Add(Map(@event, exception.Message));
            }
        });

        var response = new ProcessEventsResponseBody { Successful = successful.ToList(), Failed = failed.ToList() };

        return TypedResults.Ok(response);
    }

    private EventResponseBody Map(Event @event, string processingError = null) => new ()
    {
        Id = @event.Id,
        URL = $"{_stripeURL}/workbench/events/{@event.Id}",
        Type = @event.Type,
        CreatedUTC = @event.Created,
        ProcessingError = processingError
    };
}
