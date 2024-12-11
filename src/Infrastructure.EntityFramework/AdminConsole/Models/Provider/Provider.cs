using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models.Provider;

public class Provider : Core.AdminConsole.Entities.Provider.Provider { }

public class ProviderMapperProfile : Profile
{
    public ProviderMapperProfile()
    {
        CreateMap<Core.AdminConsole.Entities.Provider.Provider, Provider>().ReverseMap();
    }
}
