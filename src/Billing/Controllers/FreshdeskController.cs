using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using Bit.Billing.Models;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bit.Billing.Controllers;

[Route("freshdesk")]
public class FreshdeskController : Controller
{
    private readonly BillingSettings _billingSettings;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ILogger<FreshdeskController> _logger;
    private readonly GlobalSettings _globalSettings;
    private readonly IHttpClientFactory _httpClientFactory;

    public FreshdeskController(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOptions<BillingSettings> billingSettings,
        ILogger<FreshdeskController> logger,
        GlobalSettings globalSettings,
        IHttpClientFactory httpClientFactory)
    {
        _billingSettings = billingSettings?.Value;
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _logger = logger;
        _globalSettings = globalSettings;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> PostWebhook([FromQuery, Required] string key,
        [FromBody, Required] FreshdeskWebhookModel model)
    {
        if (string.IsNullOrWhiteSpace(key) || !CoreHelpers.FixedTimeEquals(key, _billingSettings.FreshdeskWebhookKey))
        {
            return new BadRequestResult();
        }

        try
        {
            var ticketId = model.TicketId;
            var ticketContactEmail = model.TicketContactEmail;
            var ticketTags = model.TicketTags;
            if (string.IsNullOrWhiteSpace(ticketId) || string.IsNullOrWhiteSpace(ticketContactEmail))
            {
                return new BadRequestResult();
            }

            var updateBody = new Dictionary<string, object>();
            var note = string.Empty;
            var customFields = new Dictionary<string, object>();
            var user = await _userRepository.GetByEmailAsync(ticketContactEmail);
            if (user != null)
            {
                var userLink = $"{_globalSettings.BaseServiceUri.Admin}/users/edit/{user.Id}";
                note += $"<li>User, {user.Email}: {userLink}</li>";
                customFields.Add("cf_user", userLink);
                var tags = new HashSet<string>();
                if (user.Premium)
                {
                    tags.Add("Premium");
                }
                var orgs = await _organizationRepository.GetManyByUserIdAsync(user.Id);

                foreach (var org in orgs)
                {
                    var orgNote = $"{org.Name} ({org.Seats.GetValueOrDefault()}): " +
                        $"{_globalSettings.BaseServiceUri.Admin}/organizations/edit/{org.Id}";
                    note += $"<li>Org, {orgNote}</li>";
                    if (!customFields.Any(kvp => kvp.Key == "cf_org"))
                    {
                        customFields.Add("cf_org", orgNote);
                    }
                    else
                    {
                        customFields["cf_org"] += $"\n{orgNote}";
                    }

                    var planName = GetAttribute<DisplayAttribute>(org.PlanType).Name.Split(" ").FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(planName))
                    {
                        tags.Add(string.Format("Org: {0}", planName));
                    }
                }
                if (tags.Any())
                {
                    var tagsToUpdate = tags.ToList();
                    if (!string.IsNullOrWhiteSpace(ticketTags))
                    {
                        var splitTicketTags = ticketTags.Split(',');
                        for (var i = 0; i < splitTicketTags.Length; i++)
                        {
                            tagsToUpdate.Insert(i, splitTicketTags[i]);
                        }
                    }
                    updateBody.Add("tags", tagsToUpdate);
                }

                if (customFields.Any())
                {
                    updateBody.Add("custom_fields", customFields);
                }
                var updateRequest = new HttpRequestMessage(HttpMethod.Put,
                    string.Format("https://bitwarden.freshdesk.com/api/v2/tickets/{0}", ticketId))
                {
                    Content = JsonContent.Create(updateBody),
                };
                await CallFreshdeskApiAsync(updateRequest);

                var noteBody = new Dictionary<string, object>
                {
                    { "body", $"<ul>{note}</ul>" },
                    { "private", true }
                };
                var noteRequest = new HttpRequestMessage(HttpMethod.Post,
                    string.Format("https://bitwarden.freshdesk.com/api/v2/tickets/{0}/notes", ticketId))
                {
                    Content = JsonContent.Create(noteBody),
                };
                await CallFreshdeskApiAsync(noteRequest);
            }

            return new OkResult();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error processing freshdesk webhook.");
            return new BadRequestResult();
        }
    }

    private async Task<HttpResponseMessage> CallFreshdeskApiAsync(HttpRequestMessage request, int retriedCount = 0)
    {
        try
        {
            var freshdeskAuthkey = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_billingSettings.FreshdeskApiKey}:X"));
            var httpClient = _httpClientFactory.CreateClient("FreshdeskApi");
            request.Headers.Add("Authorization", freshdeskAuthkey);
            var response = await httpClient.SendAsync(request);
            if (response.StatusCode != System.Net.HttpStatusCode.TooManyRequests || retriedCount > 3)
            {
                return response;
            }
        }
        catch
        {
            if (retriedCount > 3)
            {
                throw;
            }
        }
        await Task.Delay(30000 * (retriedCount + 1));
        return await CallFreshdeskApiAsync(request, retriedCount++);
    }

    private TAttribute GetAttribute<TAttribute>(Enum enumValue) where TAttribute : Attribute
    {
        return enumValue.GetType().GetMember(enumValue.ToString()).First().GetCustomAttribute<TAttribute>();
    }
}
