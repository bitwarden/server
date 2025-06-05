using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class UserSignatureKeyPair : Core.KeyManagement.Entities.UserSignatureKeyPair
{
}

public class UserSignatureKeyPairMapperProfile : Profile
{
    public UserSignatureKeyPairMapperProfile()
    {
        CreateMap<Core.KeyManagement.Entities.UserSignatureKeyPair, UserSignatureKeyPair>().ReverseMap();
    }
}
