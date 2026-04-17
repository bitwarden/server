// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class UserSignatureKeyPair : Core.KeyManagement.Entities.UserSignatureKeyPair
{
    public virtual User User { get; set; }
}

public class UserSignatureKeyPairMapperProfile : Profile
{
    public UserSignatureKeyPairMapperProfile()
    {
        CreateMap<Core.KeyManagement.Entities.UserSignatureKeyPair, UserSignatureKeyPair>().ReverseMap();
    }
}
