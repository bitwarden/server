using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class SsoConfig : Core.Models.Table.SsoConfig
    {
        public virtual Organization Organization { get; set; }
    }

    public class SsoConfigMapperProfile : Profile
    {
        public SsoConfigMapperProfile()
        {
            CreateMap<Core.Models.Table.SsoConfig, SsoConfig>().ReverseMap();
        }
    }
}
