using System.Text.Json;
using Bit.Admin.Billing.Models.StripeEvents;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Billing.Controllers;

[Authorize]
[Route("stripe-events")]
[SelfHosted(NotSelfHostedOnly = true)]
public class StripeEventsController(
    IHttpClientFactory httpClientFactory,
    IGlobalSettings globalSettings) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View(new EventIDsFormModel());
    }

    [HttpPost("process")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessAsync(EventIDsFormModel model)
    {
        var eventIds = ParseEventIDs(model.EventIDs);

        var endpoint = model.InspectOnly ? "stripe/events/inspect" : "stripe/events/process";

        var (response, failedResponseMessage) = await PostAsync(endpoint, new
        {
            eventIds
        });

        if (response == null)
        {
            return StatusCode((int)failedResponseMessage.StatusCode, "An error occurred during your request.");
        }

        response.ActionType = model.InspectOnly ? EventActionType.Inspect : EventActionType.Process;

        return View("Results", response);
    }

    private static List<string> ParseEventIDs(string eventIds) =>
        eventIds.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries).Select(id => id.Trim()).ToList();

    private async Task<(EventsResponseBody, HttpResponseMessage)> PostAsync<TRequestModel>(
        string endpoint,
        TRequestModel requestModel)
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
