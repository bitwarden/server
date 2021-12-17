using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class GroupUser : Core.Models.Table.GroupUser
    {
        public virtual Group Group { get; set; }
        public virtual OrganizationUser OrganizationUser { get; set; }
    }

    public class GroupUserMapperProfile : Profile
    {
        public GroupUserMapperProfile()
        {
            CreateMap<Core.Models.Table.GroupUser, GroupUser>().ReverseMap();
        }
    }
}

