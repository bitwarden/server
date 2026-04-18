// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models;

public class CollectionGroup : Core.Entities.CollectionGroup
{
    public virtual Collection Collection { get; set; }
    public virtual Group Group { get; set; }
}

public class CollectionGroupMapperProfile : Profile
{
    public CollectionGroupMapperProfile()
    {
        CreateMap<Core.Entities.CollectionGroup, CollectionGroup>().ReverseMap();
    }
}
