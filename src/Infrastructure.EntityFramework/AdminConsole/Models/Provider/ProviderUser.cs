// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models.Provider;

public class ProviderUser : Core.Providers.Entities.ProviderUser
{
    public virtual User User { get; set; }
    public virtual Provider Provider { get; set; }
}

public class ProviderUserMapperProfile : Profile
{
    public ProviderUserMapperProfile()
    {
        CreateMap<Core.Providers.Entities.ProviderUser, ProviderUser>().ReverseMap();
    }
}
