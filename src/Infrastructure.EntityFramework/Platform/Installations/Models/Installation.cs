using AutoMapper;
using C = Bit.Core.Platform.Installations;

namespace Bit.Infrastructure.EntityFramework.Platform;

public class Installation : C.Installation;

public class InstallationMapperProfile : Profile
{
    public InstallationMapperProfile()
    {
        CreateMap<C.Installation, Installation>().ReverseMap();
    }
}
