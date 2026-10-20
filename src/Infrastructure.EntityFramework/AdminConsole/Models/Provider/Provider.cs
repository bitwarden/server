using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models.Provider;

public class Provider : Core.Providers.Entities.Provider
{
}

public class ProviderMapperProfile : Profile
{
    public ProviderMapperProfile()
    {
        CreateMap<Core.Providers.Entities.Provider, Provider>().ReverseMap();
    }
}
