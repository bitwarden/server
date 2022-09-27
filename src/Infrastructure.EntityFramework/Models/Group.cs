using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Group : Core.Entities.Group
{
    public virtual Organization Organization { get; set; }
    public virtual ICollection<GroupUser> GroupUsers { get; set; }
}

public class GroupMapperProfile : Profile
{
    public GroupMapperProfile()
    {
        CreateMap<Core.Entities.Group, Group>().ReverseMap();
    }
}
