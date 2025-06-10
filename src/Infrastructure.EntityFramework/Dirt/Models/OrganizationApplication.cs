using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.Dirt.Models;
public class OrganizationApplication : Core.Dirt.Reports.Entities.OrganizationApplication
{
    public virtual Organization Organization { get; set; }
}

public class OrganizationApplicationProfile : Profile
{
    public OrganizationApplicationProfile()
    {
        CreateMap<Core.Dirt.Reports.Entities.OrganizationApplication, OrganizationApplication>()
            .ReverseMap();
    }
}
