using System.Text.Json.Nodes;
using Bit.Core.Enums;

#nullable enable

namespace Bit.Core.Models.Data.Organizations;

public class OrganizationIntegrationConfigurationDetails
{
    public Guid Id { get; set; }
    public Guid OrganizationIntegrationId { get; set; }
    public IntegrationType IntegrationType { get; set; }
    public EventType EventType { get; set; }
    public string? Configuration { get; set; }
    public string? IntegrationConfiguration { get; set; }
    public string? Template { get; set; }

    public JsonObject MergedConfiguration
    {
        get
        {
            var configJson = JsonNode.Parse(Configuration ?? string.Empty) as JsonObject ?? new JsonObject();
            var integrationJson = JsonNode.Parse(IntegrationConfiguration ?? string.Empty) as JsonObject ?? new JsonObject();

            foreach (var kvp in configJson)
            {
                integrationJson[kvp.Key] = kvp.Value;
            }

            return integrationJson;
        }
    }
}
