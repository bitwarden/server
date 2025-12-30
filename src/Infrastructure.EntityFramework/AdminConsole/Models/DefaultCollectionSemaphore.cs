using AutoMapper;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models;

public class DefaultCollectionSemaphore : Core.Entities.DefaultCollectionSemaphore
{
    public virtual Organization? Organization { get; set; }
    public virtual OrganizationUser? OrganizationUser { get; set; }
}

public class DefaultCollectionSemaphoreMapperProfile : Profile
{
    public DefaultCollectionSemaphoreMapperProfile()
    {
        CreateMap<Core.Entities.DefaultCollectionSemaphore, DefaultCollectionSemaphore>()
            .ForMember(dcs => dcs.Organization, opt => opt.Ignore())
            .ForMember(dcs => dcs.OrganizationUser, opt => opt.Ignore())
            .ReverseMap();
    }
}
