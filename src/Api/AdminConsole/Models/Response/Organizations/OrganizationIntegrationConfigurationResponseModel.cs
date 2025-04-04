using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;

#nullable enable

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationIntegrationConfigurationResponseModel : ResponseModel
{
    public OrganizationIntegrationConfigurationResponseModel(OrganizationIntegrationConfiguration organizationIntegrationConfiguration, string obj = "organizationIntegrationConfiguration")
        : base(obj)
    {
        if (organizationIntegrationConfiguration == null)
        {
            throw new ArgumentNullException(nameof(organizationIntegrationConfiguration));
        }

        Id = organizationIntegrationConfiguration.Id;
        Configuration = organizationIntegrationConfiguration.Configuration;
        CreationDate = organizationIntegrationConfiguration.CreationDate;
        EventType = organizationIntegrationConfiguration.EventType;
        Template = organizationIntegrationConfiguration.Template;
    }

    public Guid Id { get; set; }
    public string? Configuration { get; set; }
    public DateTime CreationDate { get; set; }
    public EventType EventType { get; set; }
    public string? Template { get; set; }
}
