// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

public class AccessDecision : Bit.Pam.Entities.AccessDecision
{
}

public class AccessDecisionMapperProfile : Profile
{
    public AccessDecisionMapperProfile()
    {
        CreateMap<Bit.Pam.Entities.AccessDecision, AccessDecision>().ReverseMap();
    }
}
