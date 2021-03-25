using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Bit.Api.Utilities
{
    public static class ApiHelpers
    {
        public async static Task<T> ReadJsonFileFromBody<T>(HttpContext httpContext, IFormFile file, long maxSize = 51200)
        {
            T obj = default(T);
            if (file != null && httpContext.Request.ContentLength.HasValue && httpContext.Request.ContentLength.Value <= maxSize)
            {
                try
                {
                    using (var stream = file.OpenReadStream())
                    using (var reader = new StreamReader(stream))
                    {
                        var s = await reader.ReadToEndAsync();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            obj = JsonConvert.DeserializeObject<T>(s);
                        }
                    }
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
        public async static Task<ObjectResult> HandleAzureEvents(HttpRequest request, string key,
            Dictionary<string, Func<EventGridEvent, Task>> eventTypeHandlers)
        {
            var queryKey = request.Query["key"];

            if (queryKey != key)
            {
                return new UnauthorizedObjectResult("Authentication failed. Please use a valid key.");
            }
            
            var response = string.Empty;
            var requestContent = await new StreamReader(request.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(requestContent))
            {
                return new OkObjectResult(response);
            }

            var eventGridSubscriber = new EventGridSubscriber();
            var eventGridEvents = eventGridSubscriber.DeserializeEventGridEvents(requestContent);

            foreach (var eventGridEvent in eventGridEvents)
            {
                if (eventGridEvent.Data is SubscriptionValidationEventData eventData)
                {
                    // Might want to enable additional validation: subject, topic etc.

                    var responseData = new SubscriptionValidationResponse()
                    {
                        ValidationResponse = eventData.ValidationCode
                    };

                    return new OkObjectResult(responseData);
                }
                else if (eventTypeHandlers.ContainsKey(eventGridEvent.EventType))
                {
                    await eventTypeHandlers[eventGridEvent.EventType](eventGridEvent);
                }
            }

            return new OkObjectResult(response);
        }
    }
}
