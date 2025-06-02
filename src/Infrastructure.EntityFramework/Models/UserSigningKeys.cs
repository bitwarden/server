using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class UserSigningKeys : Core.Entities.UserSigningKeys
{
    public virtual User User { get; set; }
}

public class UserSigningKeysMapperProfile : Profile
{
    public UserSigningKeysMapperProfile()
    {
        CreateMap<Core.Entities.UserSigningKeys, UserSigningKeys>().ReverseMap();
    }
}
