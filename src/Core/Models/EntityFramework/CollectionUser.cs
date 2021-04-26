using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class CollectionUser : Table.CollectionUser
    {
        public Collection Collection { get; set; }
        public OrganizationUser OrganizationUser { get; set; }
    }

    public class CollectionUserMapperProfile : Profile
    {
        public CollectionUserMapperProfile()
        {
            CreateMap<Table.CollectionUser, CollectionUser>().ReverseMap();
        }
    }
}
