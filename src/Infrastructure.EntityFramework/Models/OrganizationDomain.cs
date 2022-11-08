using AutoMapper;

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
