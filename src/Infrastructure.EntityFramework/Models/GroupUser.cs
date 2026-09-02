// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

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

