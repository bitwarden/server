using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class SsoUser : Core.Models.Table.SsoUser
    {
        public virtual Organization Organization { get; set; }
        public virtual User User { get; set; }
    }

    public class SsoUserMapperProfile : Profile
    {
        public SsoUserMapperProfile()
        {
            CreateMap<Core.Models.Table.SsoUser, SsoUser>().ReverseMap();
        }
    }
}
