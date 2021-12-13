using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class CollectionGroup : Core.Models.Table.CollectionGroup
    {
        public virtual Collection Collection { get; set; }
        public virtual Group Group { get; set; }
    }

    public class CollectionGroupMapperProfile : Profile
    {
        public CollectionGroupMapperProfile()
        {
            CreateMap<Core.Models.Table.CollectionGroup, CollectionGroup>().ReverseMap();
        }
    }
}
