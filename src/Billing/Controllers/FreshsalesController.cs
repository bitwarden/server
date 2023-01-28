using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bit.Billing.Controllers;

[Route("freshsales")]
public class FreshsalesController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ILogger _logger;
    private readonly GlobalSettings _globalSettings;

    private readonly string _freshsalesApiKey;

    private readonly HttpClient _httpClient;

    public FreshsalesController(IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOptions<BillingSettings> billingSettings,
        ILogger<FreshsalesController> logger,
        GlobalSettings globalSettings)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _logger = logger;
        _globalSettings = globalSettings;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://bitwarden.freshsales.io/api/")
        };

        _freshsalesApiKey = billingSettings.Value.FreshsalesApiKey;

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Token",
            $"token={_freshsalesApiKey}");
    }


    [HttpPost("webhook")]
    public async Task<IActionResult> PostWebhook([FromHeader(Name = "Authorization")] string key,
        [FromBody] CustomWebhookRequestModel request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key) || !CoreHelpers.FixedTimeEquals(_freshsalesApiKey, key))
        {
            return Unauthorized();
        }

        try
        {
            var leadResponse = await _httpClient.GetFromJsonAsync<LeadWrapper<FreshsalesLeadModel>>(
                    $"leads/{request.LeadId}",
                    cancellationToken);

            var lead = leadResponse.Lead;

            var primaryEmail = lead.Emails
                .Where(e => e.IsPrimary)
                .FirstOrDefault();

            if (primaryEmail == null)
            {
                return BadRequest(new { Message = "Lead has not primary email." });
            }

            var user = await _userRepository.GetByEmailAsync(primaryEmail.Value);

            if (user == null)
            {
                return NoContent();
            }

            var newTags = new HashSet<string>();

            if (user.Premium)
            {
                newTags.Add("Premium");
            }

            var noteItems = new List<string>
            {
                $"User, {user.Email}: {_globalSettings.BaseServiceUri.Admin}/users/edit/{user.Id}"
            };

            var orgs = await _organizationRepository.GetManyByUserIdAsync(user.Id);

            foreach (var org in orgs)
            {
                noteItems.Add($"Org, {org.Name}: {_globalSettings.BaseServiceUri.Admin}/organizations/edit/{org.Id}");
                if (TryGetPlanName(org.PlanType, out var planName))
                {
                    newTags.Add($"Org: {planName}");
                }
            }

            if (newTags.Any())
            {
                var allTags = newTags.Concat(lead.Tags);
                var updateLeadResponse = await _httpClient.PutAsJsonAsync(
                    $"leads/{request.LeadId}",
                    CreateWrapper(new { tags = allTags }),
                    cancellationToken);
                updateLeadResponse.EnsureSuccessStatusCode();
            }

            var createNoteResponse = await _httpClient.PostAsJsonAsync(
                "notes",
                CreateNoteRequestModel(request.LeadId, string.Join('\n', noteItems)), cancellationToken);
            createNoteResponse.EnsureSuccessStatusCode();
            return NoContent();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            _logger.LogError(ex, "Error processing freshsales webhook");
            return BadRequest(new { ex.Message });
        }
    }

    private static LeadWrapper<T> CreateWrapper<T>(T lead)
    {
        return new LeadWrapper<T>
        {
            Lead = lead,
        };
    }

    private static CreateNoteRequestModel CreateNoteRequestModel(long leadId, string content)
    {
        return new CreateNoteRequestModel
        {
            Note = new EditNoteModel
            {
                Description = content,
                TargetableType = "Lead",
                TargetableId = leadId,
            },
        };
    }

    private static bool TryGetPlanName(PlanType planType, out string planName)
    {
        switch (planType)
        {
            case PlanType.Free:
                planName = "Free";
                return true;
            case PlanType.FamiliesAnnually:
            case PlanType.FamiliesAnnually2019:
                planName = "Families";
                return true;
            case PlanType.TeamsAnnually:
            case PlanType.TeamsAnnually2019:
            case PlanType.TeamsMonthly:
            case PlanType.TeamsMonthly2019:
                planName = "Teams";
                return true;
            case PlanType.EnterpriseAnnually:
            case PlanType.EnterpriseAnnually2019:
            case PlanType.EnterpriseMonthly:
            case PlanType.EnterpriseMonthly2019:
                planName = "Enterprise";
                return true;
            case PlanType.Custom:
                planName = "Custom";
                return true;
            default:
                planName = null;
                return false;
        }
    }
}

public class CustomWebhookRequestModel
{
    [JsonPropertyName("leadId")]
    public long LeadId { get; set; }
}

public class LeadWrapper<T>
{
    [JsonPropertyName("lead")]
    public T Lead { get; set; }

    public static LeadWrapper<TItem> Create<TItem>(TItem lead)
    {
        return new LeadWrapper<TItem>
        {
            Lead = lead,
        };
    }
}

public class FreshsalesLeadModel
{
    public string[] Tags { get; set; }
    public FreshsalesEmailModel[] Emails { get; set; }
}

public class FreshsalesEmailModel
{
    [JsonPropertyName("value")]
    public string Value { get; set; }

    [JsonPropertyName("is_primary")]
    public bool IsPrimary { get; set; }
}

public class CreateNoteRequestModel
{
    [JsonPropertyName("note")]
    public EditNoteModel Note { get; set; }
}

public class EditNoteModel
{
    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("targetable_type")]
    public string TargetableType { get; set; }

    [JsonPropertyName("targetable_id")]
    public long TargetableId { get; set; }
}
