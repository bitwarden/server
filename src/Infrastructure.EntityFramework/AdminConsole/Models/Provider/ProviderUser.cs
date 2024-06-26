using AutoMapper;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models.Provider;

public class ProviderUser : Core.AdminConsole.Entities.Provider.ProviderUser
{
    public virtual User User { get; set; }
    public virtual Provider Provider { get; set; }
}

public class ProviderUserMapperProfile : Profile
{
    public ProviderUserMapperProfile()
    {
        CreateMap<Core.AdminConsole.Entities.Provider.ProviderUser, ProviderUser>().ReverseMap();
    }
}
