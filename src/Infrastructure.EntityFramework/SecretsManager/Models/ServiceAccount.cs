using AutoMapper;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.SecretsManager.Models;

public class ServiceAccount : Core.SecretsManager.Entities.ServiceAccount
{
    public virtual Organization Organization { get; set; }
}

public class ServiceAccountMapperProfile : Profile
{
    public ServiceAccountMapperProfile()
    {
        CreateMap<Core.SecretsManager.Entities.ServiceAccount, ServiceAccount>().ReverseMap();
    }
}
