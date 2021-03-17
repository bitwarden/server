using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class GroupUser : Table.GroupUser
    {
    }

    public class GroupUserMapperProfile : Profile
    {
        public GroupUserMapperProfile()
        {
            CreateMap<Table.GroupUser, GroupUser>().ReverseMap();
        }
    }
}
