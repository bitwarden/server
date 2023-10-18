using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Provider : Core.AdminConsole.Entities.Provider.Provider
{
}

public class ProviderMapperProfile : Profile
{
    public ProviderMapperProfile()
    {
        CreateMap<Core.AdminConsole.Entities.Provider.Provider, Provider>().ReverseMap();
    }
}
