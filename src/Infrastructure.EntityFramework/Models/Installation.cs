using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class Installation : Core.Models.Table.Installation
    {
    }

    public class InstallationMapperProfile : Profile
    {
        public InstallationMapperProfile()
        {
            CreateMap<Core.Models.Table.Installation, Installation>().ReverseMap();
        }
    }
}
