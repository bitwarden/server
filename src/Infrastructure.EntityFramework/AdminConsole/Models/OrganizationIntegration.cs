// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models;

public class OrganizationIntegration : Core.AdminConsole.Entities.OrganizationIntegration
{
    public virtual Organization Organization { get; set; }
}

public class OrganizationIntegrationMapperProfile : Profile
{
    public OrganizationIntegrationMapperProfile()
    {
        CreateMap<Core.AdminConsole.Entities.OrganizationIntegration, OrganizationIntegration>().ReverseMap();
    }
}
