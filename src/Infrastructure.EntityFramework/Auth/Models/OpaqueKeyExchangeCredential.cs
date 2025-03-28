using AutoMapper;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Auth.Models;

public class OpaqueKeyExchangeCredential : Core.Auth.Entities.OpaqueKeyExchangeCredential
{
    public virtual User User { get; set; }
}

public class OpaqueKeyExchangeCredentialMapperProfile : Profile
{
    public OpaqueKeyExchangeCredentialMapperProfile()
    {
        CreateMap<Core.Auth.Entities.OpaqueKeyExchangeCredential, OpaqueKeyExchangeCredential>().ReverseMap();
    }
}
