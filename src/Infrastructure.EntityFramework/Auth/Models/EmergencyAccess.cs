// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Auth.Models;

public class EmergencyAccess : Core.Auth.Entities.EmergencyAccess
{
    public virtual User Grantee { get; set; }
    public virtual User Grantor { get; set; }
}

public class EmergencyAccessMapperProfile : Profile
{
    public EmergencyAccessMapperProfile()
    {
        CreateMap<Core.Auth.Entities.EmergencyAccess, EmergencyAccess>().ReverseMap();
    }
}
