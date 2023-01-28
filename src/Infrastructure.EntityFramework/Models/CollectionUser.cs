using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class CollectionUser : Core.Entities.CollectionUser
{
    public virtual Collection Collection { get; set; }
    public virtual OrganizationUser OrganizationUser { get; set; }
}

public class CollectionUserMapperProfile : Profile
{
    public CollectionUserMapperProfile()
    {
        CreateMap<Core.Entities.CollectionUser, CollectionUser>().ReverseMap();
    }
}
