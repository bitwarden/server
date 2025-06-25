using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models;

public class OrganizationIntegrationConfiguration : Core.AdminConsole.Entities.OrganizationIntegrationConfiguration
{
    public virtual OrganizationIntegration OrganizationIntegration { get; set; }
}

public class OrganizationIntegrationConfigurationMapperProfile : Profile
{
    public OrganizationIntegrationConfigurationMapperProfile()
    {
        CreateMap<Core.AdminConsole.Entities.OrganizationIntegrationConfiguration, OrganizationIntegrationConfiguration>().ReverseMap();
    }
}
