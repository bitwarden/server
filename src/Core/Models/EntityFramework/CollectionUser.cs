using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class CollectionUser : Table.CollectionUser
    {
        public virtual Collection Collection { get; set; }
        public virtual OrganizationUser OrganizationUser { get; set; }
    }

    public class CollectionUserMapperProfile : Profile
    {
        public CollectionUserMapperProfile()
        {
            CreateMap<Table.CollectionUser, CollectionUser>().ReverseMap();
        }
    }
}
