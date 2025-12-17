using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models;

public class OrganizationIntegration : Core.AdminConsole.Entities.OrganizationIntegration
{
    public virtual required Organization Organization { get; set; }
}

public class OrganizationIntegrationMapperProfile : Profile
{
    public OrganizationIntegrationMapperProfile()
    {
        CreateMap<Core.AdminConsole.Entities.OrganizationIntegration, OrganizationIntegration>().ReverseMap();
    }
}
