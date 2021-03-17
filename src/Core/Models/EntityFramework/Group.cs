using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Group : Table.Group
    {
    }

    public class GroupMapperProfile : Profile
    {
        public GroupMapperProfile()
        {
            CreateMap<Table.Group, Group>().ReverseMap();
        }
    }
}
