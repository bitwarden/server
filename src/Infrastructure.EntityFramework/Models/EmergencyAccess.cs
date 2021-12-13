using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class EmergencyAccess : Core.Models.Table.EmergencyAccess
    {
        public virtual User Grantee { get; set; }
        public virtual User Grantor { get; set; }
    }

    public class EmergencyAccessMapperProfile : Profile
    {
        public EmergencyAccessMapperProfile()
        {
            CreateMap<Core.Models.Table.EmergencyAccess, EmergencyAccess>().ReverseMap();
        }
    }
}
