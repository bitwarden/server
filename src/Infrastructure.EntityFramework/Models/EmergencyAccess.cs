using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class EmergencyAccess : Core.Entities.EmergencyAccess
{
    public virtual User Grantee { get; set; }
    public virtual User Grantor { get; set; }
}

public class EmergencyAccessMapperProfile : Profile
{
    public EmergencyAccessMapperProfile()
    {
        CreateMap<Core.Entities.EmergencyAccess, EmergencyAccess>().ReverseMap();
    }
}
