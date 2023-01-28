using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class SsoUser : Core.Entities.SsoUser
{
    public virtual Organization Organization { get; set; }
    public virtual User User { get; set; }
}

public class SsoUserMapperProfile : Profile
{
    public SsoUserMapperProfile()
    {
        CreateMap<Core.Entities.SsoUser, SsoUser>().ReverseMap();
    }
}
