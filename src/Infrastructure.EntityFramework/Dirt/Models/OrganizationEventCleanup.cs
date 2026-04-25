using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Dirt.Models;

public class OrganizationEventCleanup : Core.Dirt.Entities.OrganizationEventCleanup
{
}

public class OrganizationEventCleanupProfile : Profile
{
    public OrganizationEventCleanupProfile()
    {
        CreateMap<Core.Dirt.Entities.OrganizationEventCleanup, OrganizationEventCleanup>()
            .ReverseMap();
    }
}
