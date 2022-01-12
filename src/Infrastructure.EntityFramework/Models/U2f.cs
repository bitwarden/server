using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class U2f : Core.Entities.U2f
    {
        public virtual User User { get; set; }
    }

    public class U2fMapperProfile : Profile
    {
        public U2fMapperProfile()
        {
            CreateMap<Core.Entities.U2f, U2f>().ReverseMap();
        }
    }
}
