using System.Text.Json;
using Bit.Admin.Billing.Models.ProcessStripeEvents;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Billing.Controllers;

[Authorize]
[Route("process-stripe-events")]
[SelfHosted(NotSelfHostedOnly = true)]
public class ProcessStripeEventsController(
    IHttpClientFactory httpClientFactory,
    IGlobalSettings globalSettings) : Controller
{
    [HttpGet]
    public ActionResult Index()
    {
        return View(new EventsFormModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessAsync([FromForm] EventsFormModel model)
    {
        var eventIds = model.GetEventIds();

        const string baseEndpoint = "stripe/recovery/events";

        var endpoint = model.Inspect ? $"{baseEndpoint}/inspect" : $"{baseEndpoint}/process";

        var (response, failedResponseMessage) = await PostAsync(endpoint, new EventsRequestBody
        {
            EventIds = eventIds
        });

        if (response == null)
        {
            return StatusCode((int)failedResponseMessage.StatusCode, "An error occurred during your request.");
        }

        response.ActionType = model.Inspect ? EventActionType.Inspect : EventActionType.Process;

        return View("Results", response);
    }

    private async Task<(EventsResponseBody, HttpResponseMessage)> PostAsync(
        string endpoint,
        EventsRequestBody requestModel)
    {
        var client = httpClientFactory.CreateClient("InternalBilling");
        client.BaseAddress = new Uri(globalSettings.BaseServiceUri.InternalBilling);

        var json = JsonSerializer.Serialize(requestModel);
        var requestBody = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var responseMessage = await client.PostAsync(endpoint, requestBody);

        if (!responseMessage.IsSuccessStatusCode)
        {
            return (null, responseMessage);
        }

        var responseContent = await responseMessage.Content.ReadAsStringAsync();

        var response = JsonSerializer.Deserialize<EventsResponseBody>(responseContent);

        return (response, null);
    }
}
