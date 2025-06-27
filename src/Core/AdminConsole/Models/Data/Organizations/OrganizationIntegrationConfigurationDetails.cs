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
    public string? Filters { get; set; }
    public string? IntegrationConfiguration { get; set; }
    public string? Template { get; set; }

    public JsonObject MergedConfiguration
    {
        get
        {
            var integrationJson = IntegrationConfigurationJson;

            foreach (var kvp in ConfigurationJson)
            {
                integrationJson[kvp.Key] = kvp.Value?.DeepClone();
            }

            return integrationJson;
        }
    }

    private JsonObject ConfigurationJson
    {
        get
        {
            try
            {
                var configuration = Configuration ?? string.Empty;
                return JsonNode.Parse(configuration) as JsonObject ?? new JsonObject();
            }
            catch
            {
                return new JsonObject();
            }
        }
    }

    private JsonObject IntegrationConfigurationJson
    {
        get
        {
            try
            {
                var integration = IntegrationConfiguration ?? string.Empty;
                return JsonNode.Parse(integration) as JsonObject ?? new JsonObject();
            }
            catch
            {
                return new JsonObject();
            }
        }
    }
}
