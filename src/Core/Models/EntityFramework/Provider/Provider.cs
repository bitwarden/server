using AutoMapper;

namespace Bit.Core.Models.EntityFramework.Provider
{
    public class Provider : Table.Provider.Provider
    {
    }

    public class ProviderMapperProfile : Profile
    {
        public ProviderMapperProfile()
        {
            CreateMap<Table.Provider.Provider, Provider>().ReverseMap();
        }
    }
}
