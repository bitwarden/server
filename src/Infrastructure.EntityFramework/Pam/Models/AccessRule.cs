// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

public class AccessRule : Bit.Pam.Entities.AccessRule
{
    public virtual Organization Organization { get; set; }
}

public class AccessRuleMapperProfile : Profile
{
    public AccessRuleMapperProfile()
    {
        CreateMap<Bit.Pam.Entities.AccessRule, AccessRule>().ReverseMap();
        CreateMap<AccessRule, Bit.Pam.Models.AccessRuleDetails>();
    }
}
