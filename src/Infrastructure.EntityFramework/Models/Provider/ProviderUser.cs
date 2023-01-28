using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class ProviderUser : Core.Entities.Provider.ProviderUser
{
    public virtual User User { get; set; }
    public virtual Provider Provider { get; set; }
}

public class ProviderUserMapperProfile : Profile
{
    public ProviderUserMapperProfile()
    {
        CreateMap<Core.Entities.Provider.ProviderUser, ProviderUser>().ReverseMap();
    }
}
