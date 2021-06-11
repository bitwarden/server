using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Group : Table.Group
    {
        public virtual Organization Organization { get; set; }
        public virtual ICollection<GroupUser> GroupUsers { get; set; }
    }

    public class GroupMapperProfile : Profile
    {
        public GroupMapperProfile()
        {
            CreateMap<Table.Group, Group>().ReverseMap();
        }
    }
}
