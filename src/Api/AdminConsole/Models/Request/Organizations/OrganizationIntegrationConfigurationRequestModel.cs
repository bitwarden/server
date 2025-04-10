using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;

#nullable enable

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationIntegrationConfigurationRequestModel : IValidatableObject
{
    public string? Configuration { get; set; }

    [Required]
    public EventType EventType { get; set; }

    public string? Template { get; set; }

    public OrganizationIntegrationConfiguration ToOrganizationIntegrationConfiguration(Guid organizationIntegrationId)
    {
        return new OrganizationIntegrationConfiguration()
        {
            OrganizationIntegrationId = organizationIntegrationId,
            Configuration = Configuration,
            EventType = EventType,
            Template = Template
        };
    }

    public OrganizationIntegrationConfiguration ToOrganizationIntegrationConfiguration(OrganizationIntegrationConfiguration currentConfiguration)
    {
        currentConfiguration.Configuration = Configuration;
        currentConfiguration.EventType = EventType;
        currentConfiguration.Template = Template;

        return currentConfiguration;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return [];
    }
}
