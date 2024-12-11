using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Installation : Core.Entities.Installation { }

public class InstallationMapperProfile : Profile
{
    public InstallationMapperProfile()
    {
        CreateMap<Core.Entities.Installation, Installation>().ReverseMap();
    }
}
