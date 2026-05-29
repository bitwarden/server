using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.Dirt.Models;

public class OrganizationIntegration : Core.Dirt.Entities.OrganizationIntegration
{
    public virtual required Organization Organization { get; set; }
}

public class OrganizationIntegrationMapperProfile : Profile
{
    public OrganizationIntegrationMapperProfile()
    {
        CreateMap<Core.Dirt.Entities.OrganizationIntegration, OrganizationIntegration>().ReverseMap();
    }
}
