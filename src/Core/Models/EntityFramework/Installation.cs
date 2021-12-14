using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Installation : Table.Installation
    {
    }

    public class InstallationMapperProfile : Profile
    {
        public InstallationMapperProfile()
        {
            CreateMap<Table.Installation, Installation>().ReverseMap();
        }
    }
}
