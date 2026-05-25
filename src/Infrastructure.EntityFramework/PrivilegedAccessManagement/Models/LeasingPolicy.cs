// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.PrivilegedAccessManagement.Models;

public class LeasingPolicy : Core.PrivilegedAccessManagement.Entities.LeasingPolicy
{
    public virtual Organization Organization { get; set; }
}

public class LeasingPolicyMapperProfile : Profile
{
    public LeasingPolicyMapperProfile()
    {
        CreateMap<Core.PrivilegedAccessManagement.Entities.LeasingPolicy, LeasingPolicy>().ReverseMap();
    }
}
