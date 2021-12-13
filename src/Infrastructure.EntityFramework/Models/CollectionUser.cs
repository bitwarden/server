using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class CollectionUser : Core.Models.Table.CollectionUser
    {
        public virtual Collection Collection { get; set; }
        public virtual OrganizationUser OrganizationUser { get; set; }
    }

    public class CollectionUserMapperProfile : Profile
    {
        public CollectionUserMapperProfile()
        {
            CreateMap<Core.Models.Table.CollectionUser, CollectionUser>().ReverseMap();
        }
    }
}
