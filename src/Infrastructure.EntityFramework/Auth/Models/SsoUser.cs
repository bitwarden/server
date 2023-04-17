using AutoMapper;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Auth.Models;

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
