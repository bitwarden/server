using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class UserSigningKeys : Core.Entities.UserSignatureKeyPair
{
    public virtual User User { get; set; }
}

public class UserSigningKeysMapperProfile : Profile
{
    public UserSigningKeysMapperProfile()
    {
        CreateMap<Core.Entities.UserSignatureKeyPair, UserSigningKeys>().ReverseMap();
    }
}
