using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Provider : Core.Entities.Provider.Provider
{
}

public class ProviderMapperProfile : Profile
{
    public ProviderMapperProfile()
    {
        CreateMap<Core.Entities.Provider.Provider, Provider>().ReverseMap();
    }
}
