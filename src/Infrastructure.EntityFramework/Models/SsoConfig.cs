using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class SsoConfig : Core.Auth.Entities.SsoConfig
{
    public virtual Organization Organization { get; set; }
}

public class SsoConfigMapperProfile : Profile
{
    public SsoConfigMapperProfile()
    {
        CreateMap<Core.Auth.Entities.SsoConfig, SsoConfig>().ReverseMap();
    }
}
