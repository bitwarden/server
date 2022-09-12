using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

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
