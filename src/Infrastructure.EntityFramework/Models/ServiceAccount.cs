using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class ServiceAccount : Core.Entities.ServiceAccount
{
    public virtual Organization Organization { get; set; }
}

public class ServiceAccountMapperProfile : Profile
{
    public ServiceAccountMapperProfile()
    {
        CreateMap<Core.Entities.ServiceAccount, ServiceAccount>().ReverseMap();
    }
}
