using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class Grant : Core.Models.Table.Grant
    {
    }

    public class GrantMapperProfile : Profile
    {
        public GrantMapperProfile()
        {
            CreateMap<Core.Models.Table.Grant, Grant>().ReverseMap();
        }
    }
}
