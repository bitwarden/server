﻿using System.Text.Json;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Utilities;

public static class ApiHelpers
{
    public static string EventGridKey { get; set; }
    public async static Task<T> ReadJsonFileFromBody<T>(HttpContext httpContext, IFormFile file, long maxSize = 51200)
    {
        T obj = default(T);
        if (file != null && httpContext.Request.ContentLength.HasValue && httpContext.Request.ContentLength.Value <= maxSize)
        {
            try
            {
                using var stream = file.OpenReadStream();
                obj = await JsonSerializer.DeserializeAsync<T>(stream, JsonHelpers.IgnoreCase);
            }
            catch { }
        }

        return obj;
    }

    /// <summary>
    /// Validates Azure event subscription and calls the appropriate event handler. Responds HttpOk.
    /// </summary>
    /// <param name="request">HttpRequest received from Azure</param>
    /// <param name="eventTypeHandlers">Dictionary of eventType strings and their associated handlers.</param>
    /// <returns>OkObjectResult</returns>
    /// <remarks>Reference https://docs.microsoft.com/en-us/azure/event-grid/receive-events</remarks>
    public async static Task<ObjectResult> HandleAzureEvents(HttpRequest request,
        Dictionary<string, Func<EventGridEvent, Task>> eventTypeHandlers)
    {
        var queryKey = request.Query["key"];

        if (!CoreHelpers.FixedTimeEquals(queryKey, EventGridKey))
        {
            return new UnauthorizedObjectResult("Authentication failed. Please use a valid key.");
        }

        var response = string.Empty;
        var requestData = await BinaryData.FromStreamAsync(request.Body);
        var eventGridEvents = EventGridEvent.ParseMany(requestData);
        foreach (var eventGridEvent in eventGridEvents)
        {
            if (eventGridEvent.TryGetSystemEventData(out object systemEvent))
            {
                if (systemEvent is SubscriptionValidationEventData eventData)
                {
                    // Might want to enable additional validation: subject, topic etc.
                    var responseData = new SubscriptionValidationResponse()
                    {
                        ValidationResponse = eventData.ValidationCode
                    };

                    return new OkObjectResult(responseData);
                }
            }

            if (eventTypeHandlers.TryGetValue(eventGridEvent.EventType, out var eventTypeHandler))
            {
                await eventTypeHandler(eventGridEvent);
            }
        }

        return new OkObjectResult(response);
    }

    /// <summary>
    /// Validates and returns a date range. Currently used for fetching events.
    /// </summary>
    /// <param name="start">start date and time</param>
    /// <param name="end">end date and time</param>
    /// <remarks>
    /// If start or end are null, will return a range of the last 30 days.
    /// If a time span greater than 367 days is passed will throw BadRequestException.
    /// </remarks>
    public static Tuple<DateTime, DateTime> GetDateRange(DateTime? start, DateTime? end)
    {
        if (!end.HasValue || !start.HasValue)
        {
            end = DateTime.UtcNow.Date.AddDays(1).AddMilliseconds(-1);
            start = DateTime.UtcNow.Date.AddDays(-30);
        }
        else if (start.Value > end.Value)
        {
            var newEnd = start;
            start = end;
            end = newEnd;
        }

        if ((end.Value - start.Value) > TimeSpan.FromDays(367))
        {
            throw new BadRequestException("Range too large.");
        }

        return new Tuple<DateTime, DateTime>(start.Value, end.Value);
    }
}
