using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class GroupUser : Core.AdminConsole.Entities.GroupUser
{
    public virtual Group Group { get; set; }
    public virtual OrganizationUser OrganizationUser { get; set; }
}

public class GroupUserMapperProfile : Profile
{
    public GroupUserMapperProfile()
    {
        CreateMap<Core.AdminConsole.Entities.GroupUser, GroupUser>().ReverseMap();
    }
}
