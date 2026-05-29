using Bit.Core.Dirt.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Api.Dirt.Models.Response;

public class OrganizationIntegrationConfigurationResponseModel : ResponseModel
{
    public OrganizationIntegrationConfigurationResponseModel(OrganizationIntegrationConfiguration organizationIntegrationConfiguration, string obj = "organizationIntegrationConfiguration")
        : base(obj)
    {
        Id = organizationIntegrationConfiguration.Id;
        Configuration = organizationIntegrationConfiguration.Configuration;
        CreationDate = organizationIntegrationConfiguration.CreationDate;
        EventType = organizationIntegrationConfiguration.EventType;
        Filters = organizationIntegrationConfiguration.Filters;
        Template = organizationIntegrationConfiguration.Template;
    }

    public Guid Id { get; set; }
    public string? Configuration { get; set; }
    public string? Filters { get; set; }
    public DateTime CreationDate { get; set; }
    public EventType? EventType { get; set; }
    public string? Template { get; set; }
}
