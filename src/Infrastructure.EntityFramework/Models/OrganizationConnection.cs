using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class OrganizationConnection : Core.Entities.OrganizationConnection
{
    public virtual Organization Organization { get; set; }
}

public class OrganizationConnectionMapperProfile : Profile
{
    public OrganizationConnectionMapperProfile()
    {
        CreateMap<Core.Entities.OrganizationConnection, OrganizationConnection>().ReverseMap();
    }
}
