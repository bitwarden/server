// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.Dirt.Models;
public class OrganizationApplication : Core.Dirt.Entities.OrganizationApplication
{
    public virtual Organization Organization { get; set; }
}

public class OrganizationApplicationProfile : Profile
{
    public OrganizationApplicationProfile()
    {
        CreateMap<Core.Dirt.Entities.OrganizationApplication, OrganizationApplication>()
            .ReverseMap();
    }
}
