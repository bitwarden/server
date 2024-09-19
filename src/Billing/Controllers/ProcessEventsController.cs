using System.Collections.Concurrent;
using Bit.Billing.Constants;
using Bit.Billing.Models;
using Bit.Billing.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Bit.Billing.Controllers;

[Route("stripe/process-events")]
[SelfHosted(NotSelfHostedOnly = true)]
public class ProcessEventsController(
    IStripeEventProcessor stripeEventProcessor,
    IStripeFacade stripeFacade) : Controller
{
    [HttpPost("processable")]
    public async Task<Ok<ProcessedEventsResponseBody>> CheckProcessableEventsAsync([FromBody] ProcessEventsRequestBody requestBody)
    {
        var stripeEvents = await GetProcessableEventsAsync(requestBody);

        var processed = stripeEvents.Select(stripeEvent => ProcessedEvent.From(stripeEvent)).ToList();

        var response = new ProcessedEventsResponseBody { Success = processed, Failed = [] };

        return TypedResults.Ok(response);
    }

    [HttpPost]
    public async Task<Ok<ProcessedEventsResponseBody>> ProcessEventsAsync([FromBody] ProcessEventsRequestBody requestBody)
    {
        var stripeEvents = await GetProcessableEventsAsync(requestBody);

        var success = new ConcurrentBag<ProcessedEvent>();

        var failed = new ConcurrentBag<ProcessedEvent>();

        await Parallel.ForEachAsync(stripeEvents, async (stripeEvent, _) =>
        {
            try
            {
                await stripeEventProcessor.ProcessEventAsync(stripeEvent);

                success.Add(ProcessedEvent.From(stripeEvent));
            }
            catch (Exception exception)
            {
                failed.Add(ProcessedEvent.From(stripeEvent, exception.Message));
            }
        });

        var response = new ProcessedEventsResponseBody
        {
            Success = success.ToList(),
            Failed = failed.ToList(),
        };

        return TypedResults.Ok(response);
    }

    private async Task<List<Event>> GetProcessableEventsAsync(ProcessEventsRequestBody requestBody)
    {
        var eventListOptions = new EventListOptions
        {
            Created = new DateRangeOptions { GreaterThanOrEqual = requestBody.From, LessThanOrEqual = requestBody.To },
            DeliverySuccess = requestBody.DeliverySuccess,
            Types =
            [
                HandledStripeWebhook.ChargeRefunded,
                HandledStripeWebhook.ChargeSucceeded,
                HandledStripeWebhook.SubscriptionDeleted,
                HandledStripeWebhook.SubscriptionUpdated,
                HandledStripeWebhook.CustomerUpdated,
                HandledStripeWebhook.InvoiceCreated,
                HandledStripeWebhook.InvoiceFinalized,
                HandledStripeWebhook.PaymentFailed,
                HandledStripeWebhook.PaymentSucceeded,
                HandledStripeWebhook.UpcomingInvoice,
                HandledStripeWebhook.PaymentMethodAttached
            ]
        };

        var process = new List<Event>();

        var events = stripeFacade.ListEvents(eventListOptions);

        await foreach (var @event in events)
        {
            if (await CanProcessEventAsync(@event, requestBody.APIVersion, requestBody.Region))
            {
                process.Add(@event);
            }
        }

        return process;
    }

    private async Task<Customer> GetCustomerAsync(Event @event) => @event.Data.Object switch
    {
        Charge charge => !string.IsNullOrEmpty(charge.CustomerId) ? await stripeFacade.GetCustomer(charge.CustomerId) : null,
        Customer customer => customer,
        Invoice invoice => !string.IsNullOrEmpty(invoice.CustomerId) ? await stripeFacade.GetCustomer(invoice.CustomerId) : null,
        PaymentMethod paymentMethod => !string.IsNullOrEmpty(paymentMethod.CustomerId) ? await stripeFacade.GetCustomer(paymentMethod.CustomerId) : null,
        Subscription subscription => !string.IsNullOrEmpty(subscription.CustomerId) ? await stripeFacade.GetCustomer(subscription.CustomerId) : null,
        _ => null
    };

    private async Task<bool> CanProcessEventAsync(Event @event, string apiVersion, string region)
    {
        if (@event.ApiVersion != apiVersion)
        {
            return false;
        }

        var customer = await GetCustomerAsync(@event);

        // Customers without regions default to "US"
        if (customer?.Metadata == null || !customer.Metadata.ContainsKey("region") && region == "US")
        {
            return true;
        }

        return customer.Metadata != null && customer.Metadata.TryGetValue("region", out var value) && value == region;
    }
}
