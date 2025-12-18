using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Dirt.Models;

public class OrganizationIntegrationConfiguration : Core.Dirt.Entities.OrganizationIntegrationConfiguration
{
    public virtual required OrganizationIntegration OrganizationIntegration { get; set; }
}

public class OrganizationIntegrationConfigurationMapperProfile : Profile
{
    public OrganizationIntegrationConfigurationMapperProfile()
    {
        CreateMap<Core.Dirt.Entities.OrganizationIntegrationConfiguration, OrganizationIntegrationConfiguration>().ReverseMap();
    }
}
