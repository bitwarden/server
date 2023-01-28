using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class SsoConfig : Core.Entities.SsoConfig
{
    public virtual Organization Organization { get; set; }
}

public class SsoConfigMapperProfile : Profile
{
    public SsoConfigMapperProfile()
    {
        CreateMap<Core.Entities.SsoConfig, SsoConfig>().ReverseMap();
    }
}
