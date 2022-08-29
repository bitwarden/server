using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class GroupUser : Core.Entities.GroupUser
{
    public virtual Group Group { get; set; }
    public virtual OrganizationUser OrganizationUser { get; set; }
}

public class GroupUserMapperProfile : Profile
{
    public GroupUserMapperProfile()
    {
        CreateMap<Core.Entities.GroupUser, GroupUser>().ReverseMap();
    }
}

