using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Group : Core.AdminConsole.Entities.Group
{
    public virtual Organization Organization { get; set; }
    public virtual ICollection<GroupUser> GroupUsers { get; set; }
}

public class GroupMapperProfile : Profile
{
    public GroupMapperProfile()
    {
        CreateMap<Core.AdminConsole.Entities.Group, Group>().ReverseMap();
    }
}
