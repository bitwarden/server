using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class EmergencyAccess : Table.EmergencyAccess
    {
    }

    public class EmergencyAccessMapperProfile : Profile
    {
        public EmergencyAccessMapperProfile()
        {
            CreateMap<Table.EmergencyAccess, EmergencyAccess>().ReverseMap();
        }
    }
}
