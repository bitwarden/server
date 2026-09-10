// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

public class AccessLease : Bit.Pam.Entities.AccessLease
{
    public virtual Organization Organization { get; set; }
}

public class AccessLeaseMapperProfile : Profile
{
    public AccessLeaseMapperProfile()
    {
        CreateMap<Bit.Pam.Entities.AccessLease, AccessLease>().ReverseMap();
    }
}
