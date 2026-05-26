// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.PrivilegedAccessManagement.Models;

public class AccessRule : Core.PrivilegedAccessManagement.Entities.AccessRule
{
    public virtual Organization Organization { get; set; }
}

public class AccessRuleMapperProfile : Profile
{
    public AccessRuleMapperProfile()
    {
        CreateMap<Core.PrivilegedAccessManagement.Entities.AccessRule, AccessRule>().ReverseMap();
    }
}
