using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class GroupUser : Table.GroupUser
    {
        public virtual Group Group { get; set; }
        public virtual OrganizationUser OrganizationUser { get; set; }
    }

    public class GroupUserMapperProfile : Profile
    {
        public GroupUserMapperProfile()
        {
            CreateMap<Table.GroupUser, GroupUser>().ReverseMap();
        }
    }
}

