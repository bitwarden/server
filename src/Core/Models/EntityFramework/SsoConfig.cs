using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class SsoConfig : Table.SsoConfig
    {
        public virtual Organization Organization { get; set; }
    }

    public class SsoConfigMapperProfile : Profile
    {
        public SsoConfigMapperProfile()
        {
            CreateMap<Table.SsoConfig, SsoConfig>().ReverseMap();
        }
    }
}
