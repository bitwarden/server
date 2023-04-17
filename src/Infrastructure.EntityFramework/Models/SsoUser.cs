using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class SsoUser : Core.Auth.Entities.SsoUser
{
    public virtual Organization Organization { get; set; }
    public virtual User User { get; set; }
}

public class SsoUserMapperProfile : Profile
{
    public SsoUserMapperProfile()
    {
        CreateMap<Core.Auth.Entities.SsoUser, SsoUser>().ReverseMap();
    }
}
