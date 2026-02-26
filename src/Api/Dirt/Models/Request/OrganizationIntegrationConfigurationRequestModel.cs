using Bit.Core.Dirt.Entities;
using Bit.Core.Enums;

namespace Bit.Api.Dirt.Models.Request;

public class OrganizationIntegrationConfigurationRequestModel
{
    public string? Configuration { get; set; }

    public EventType? EventType { get; set; }

    public string? Filters { get; set; }

    public string? Template { get; set; }

    public OrganizationIntegrationConfiguration ToOrganizationIntegrationConfiguration(Guid organizationIntegrationId)
    {
        return new OrganizationIntegrationConfiguration()
        {
            OrganizationIntegrationId = organizationIntegrationId,
            Configuration = Configuration,
            Filters = Filters,
            EventType = EventType,
            Template = Template
        };
    }
}
