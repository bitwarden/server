using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class CollectionGroup : Table.CollectionGroup
    {
        public Collection Collection { get; set; }
        public Group Group { get; set; }
    }

    public class CollectionGroupMapperProfile : Profile
    {
        public CollectionGroupMapperProfile()
        {
            CreateMap<Table.CollectionGroup, CollectionGroup>().ReverseMap();
        }
    }
}
