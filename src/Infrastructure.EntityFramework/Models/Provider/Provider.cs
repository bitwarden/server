using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class Provider : Core.Models.Table.Provider.Provider
    {
    }

    public class ProviderMapperProfile : Profile
    {
        public ProviderMapperProfile()
        {
            CreateMap<Core.Models.Table.Provider.Provider, Provider>().ReverseMap();
        }
    }
}
