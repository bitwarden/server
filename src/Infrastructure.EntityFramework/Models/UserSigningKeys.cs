using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class UserSignatureKeyPair : Core.Entities.UserSignatureKeyPair
{
    public virtual User User { get; set; }
}

public class UserSignatureKeyPairMapperProfile : Profile
{
    public UserSignatureKeyPairMapperProfile()
    {
        CreateMap<Core.Entities.UserSignatureKeyPair, UserSignatureKeyPair>().ReverseMap();
    }
}
