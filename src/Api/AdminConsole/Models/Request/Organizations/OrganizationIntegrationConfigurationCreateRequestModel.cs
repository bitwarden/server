using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;

#nullable enable

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationIntegrationConfigurationCreateRequestModel : IValidatableObject
{
    public string? Configuration { get; set; }

    [Required]
    public Guid OrganizationIntegrationId { get; set; }

    [Required]
    public EventType EventType { get; set; }

    public string? Template { get; set; }

    public OrganizationIntegrationConfiguration ToOrganizationIntegrationConfiguration()
    {
        return new OrganizationIntegrationConfiguration()
        {
            OrganizationIntegrationId = OrganizationIntegrationId,
            Configuration = Configuration,
            EventType = EventType,
            Template = Template
        };
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return [];
    }
}
