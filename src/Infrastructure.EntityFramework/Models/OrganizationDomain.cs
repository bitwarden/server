// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.Models;

public class OrganizationDomain : Core.Entities.OrganizationDomain
{
    public virtual Organization Organization { get; set; }
}

public class OrganizationDomainMapperProfile : Profile
{
    public OrganizationDomainMapperProfile()
    {
        CreateMap<Core.Entities.OrganizationDomain, OrganizationDomain>().ReverseMap();
    }
}
