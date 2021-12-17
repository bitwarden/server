using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class EmergencyAccess : Table.EmergencyAccess
    {
        public virtual User Grantee { get; set; }
        public virtual User Grantor { get; set; }
    }

    public class EmergencyAccessMapperProfile : Profile
    {
        public EmergencyAccessMapperProfile()
        {
            CreateMap<Table.EmergencyAccess, EmergencyAccess>().ReverseMap();
        }
    }
}
